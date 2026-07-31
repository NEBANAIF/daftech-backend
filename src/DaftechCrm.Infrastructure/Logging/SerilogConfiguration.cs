using Serilog;
using Serilog.Events;

namespace DaftechCrm.Infrastructure.Logging;

/// <summary>
/// Central Serilog setup: structured logging to the console (container-friendly)
/// and to rotating daily files under <c>logs/</c>. Anything in the
/// <c>Serilog</c> configuration section wins over these defaults.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>Builds the logger used by the host.</summary>
    /// <param name="configuration">App configuration (read for the optional "Serilog" section).</param>
    /// <param name="environmentName">Environment name, written onto every event.</param>
    public static ILogger Create(Microsoft.Extensions.Configuration.IConfiguration configuration, string environmentName)
    {
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

        // Containers often have a read-only or ephemeral working directory; LOG_DIR
        // lets the platform point file logging at a writable path (or disable it).
        var logDirectory = Environment.GetEnvironmentVariable("LOG_DIR") ?? "logs";
        var fileLoggingEnabled = !string.Equals(logDirectory, "off", StringComparison.OrdinalIgnoreCase);

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(isProduction ? LogEventLevel.Information : LogEventLevel.Debug)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.With(new LogEnricher())
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.Conditional(_ => fileLoggingEnabled, sink => sink.File(
                path: Path.Combine(logDirectory, "daftech-crm-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate:
                    "{Timestamp:o} [{Level:u3}] ({Application}/{MachineName}/{ProcessId}/{ThreadId}) {Message:lj}{NewLine}{Exception}"))
            .ReadFrom.Configuration(configuration);

        return loggerConfiguration.CreateLogger();
    }
}
