using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Application.Services;
using DaftechCrm.Infrastructure.Ai;
using DaftechCrm.Infrastructure.Email;
using DaftechCrm.Infrastructure.Persistence;
using DaftechCrm.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DaftechCrm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = PostgresConnectionString.Resolve(configuration);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            }));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<TicketWorkflowOptions>(configuration.GetSection(TicketWorkflowOptions.SectionName));
        services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, MailKitEmailSender>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddScoped<AccountCredentialService>();
        services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IAgreementService, AgreementService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<ITimeLogService, TimeLogService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISessionService, SessionService>();

        services.Configure<AiReportingOptions>(configuration.GetSection(AiReportingOptions.SectionName));
        services.AddHttpClient<IAiNarrativeReportService, AnthropicNarrativeReportService>();
        services.AddScoped<ISatisfactionSurveyService, SatisfactionSurveyService>();

        return services;
    }

    /// <summary>Applies pending migrations and inserts seed data if the database is empty. Call once at startup.</summary>
    public static async Task MigrateAndSeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Managed databases (e.g. Render) can still be booting when the API starts.
        await WaitForDatabaseAsync(db);

        // Use migrations when the assembly ships any; otherwise create the schema
        // directly so a fresh deployment comes up with a usable database.
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (!await db.EmployeesSet.AnyAsync())
        {
            db.EmployeesSet.AddRange(SeedData.Employees());
            db.ClientsSet.AddRange(SeedData.Clients());
            await db.SaveChangesAsync();
        }
    }

    private static async Task WaitForDatabaseAsync(AppDbContext db, int attempts = 10)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            if (await db.Database.CanConnectAsync()) return;
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 10)));
        }

        throw new InvalidOperationException("The PostgreSQL database is not reachable. Check DATABASE_URL.");
    }
}
