using System.Text;
using DaftechCrm.Api.BackgroundServices;
using DaftechCrm.Api.Extensions;
using DaftechCrm.Api.Middleware;
using DaftechCrm.Api.Services;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Infrastructure;
using DaftechCrm.Infrastructure.Extensions;
using DaftechCrm.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Bootstrap logger: captures failures that happen before the host is built.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    const string AngularCorsPolicy = "AngularClient";

    // ---- Structured logging (Serilog: console + rotating files) ----
    Log.Logger = SerilogConfiguration.Create(builder.Configuration, builder.Environment.EnvironmentName);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();

    builder.Services.AddInfrastructure(builder.Configuration);

    // ---- Authentication / authorization ----
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");
    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey must be set to a secret of at least 32 bytes (set via user-secrets/environment, not appsettings.json).");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddCrmAuthorizationPolicies();

    // ---- Cross-cutting hardening / observability ----
    builder.Services.AddSecurityHardening();
    builder.Services.AddCrmRateLimiting();
    builder.Services.AddCrmHealthChecks();

    builder.Services.AddHostedService<AutoCloseTicketsHostedService>();
    builder.Services.AddHostedService<SessionSweepHostedService>();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:4200"];

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

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseSecurityHardening();
    app.UseCors(AngularCorsPolicy);
    app.UseRateLimiter();

    // Ensure the upload root exists — files are no longer served via UseStaticFiles
    // (that ran unauthenticated ahead of the auth middleware, so anyone with a
    // guessed/leaked path could read agreement documents with no login at all).
    // Downloads now go exclusively through AgreementsController.DownloadDocument,
    // which is [Authorize]'d and checked against the current account.
    var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
    Directory.CreateDirectory(Path.GetFullPath(storageOptions.RootPath));

    // UseAuthentication must run before UseAuthorization or every [Authorize]
    // check below sees an unauthenticated user regardless of the token sent.
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapCrmHealthChecks();

    // Apply pending EF Core migrations and seed baseline data on startup.
    await app.Services.MigrateAndSeedAsync();

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
