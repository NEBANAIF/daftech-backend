using DaftechCrm.Api.BackgroundServices;
using DaftechCrm.Api.Extensions;
using DaftechCrm.Api.Middleware;
using DaftechCrm.Api.Services;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Infrastructure;
using DaftechCrm.Infrastructure.Extensions;
using DaftechCrm.Infrastructure.Logging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Serilog;

// Bootstrap logger: captures failures that happen before the host is built.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    const string AngularCorsPolicy = "AngularClient";

    // ---- Hosting (Render/containers): bind to the port the platform injects ----
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrWhiteSpace(port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }

    // Render terminates TLS at its proxy, so trust the forwarded scheme/host headers.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ---- Structured logging (Serilog: console + rotating files) ----
    Log.Logger = SerilogConfiguration.Create(builder.Configuration, builder.Environment.EnvironmentName);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();

    builder.Services.AddInfrastructure(builder.Configuration);

    // ---- Cross-cutting hardening / observability ----
    builder.Services.AddSecurityHardening();
    builder.Services.AddCrmRateLimiting();
    builder.Services.AddCrmHealthChecks();

    builder.Services.AddHostedService<AutoCloseTicketsHostedService>();
    builder.Services.AddHostedService<SessionSweepHostedService>();

    // CORS origins come from CORS_ALLOWED_ORIGINS (comma separated) when set, so a
    // deployment can be re-pointed without a rebuild; otherwise from configuration.
    var originsFromEnv = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var allowedOrigins = originsFromEnv.Length > 0
        ? originsFromEnv
        : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(AngularCorsPolicy, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Swagger stays available in hosted environments unless explicitly disabled,
    // so a deployed API can be verified without a local build.
    if (app.Environment.IsDevelopment() ||
        app.Configuration.GetValue("Swagger:Enabled", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseSecurityHardening();
    app.UseCors(AngularCorsPolicy);
    app.UseRateLimiter();

    // Serve uploaded agreement documents from the configured storage root.
    var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
    var storageRoot = Path.GetFullPath(storageOptions.RootPath);
    Directory.CreateDirectory(storageRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(storageRoot),
        RequestPath = storageOptions.PublicBaseUrl.TrimEnd('/'),
        ServeUnknownFileTypes = false,
    });

    app.UseAuthorization();
    app.MapControllers();
    app.MapCrmHealthChecks();

    // Apply pending EF Core migrations and seed baseline data on startup.
    // A database hiccup must not stop the process: the health endpoints report it
    // and the platform keeps the instance alive instead of crash-looping.
    try
    {
        await app.Services.MigrateAndSeedAsync();
    }
    catch (Exception dbEx)
    {
        Log.Error(dbEx, "Database migration/seeding failed at startup; the API will continue to start");
    }

    Log.Information("DAFTECH CRM API starting in {Environment}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DAFTECH CRM API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
