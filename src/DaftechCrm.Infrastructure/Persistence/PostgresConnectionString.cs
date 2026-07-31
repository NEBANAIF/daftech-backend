using Microsoft.Extensions.Configuration;

namespace DaftechCrm.Infrastructure.Persistence;

/// <summary>
/// Resolves the PostgreSQL connection string from configuration or the environment.
///
/// Managed hosts (Render, Railway, Heroku, Supabase, Neon…) expose the database as a
/// URL such as <c>postgres://user:pass@host:5432/dbname</c>, which Npgsql does not
/// understand. This converts that URL form into a proper key/value connection string
/// and always enables SSL for non-local hosts.
/// </summary>
public static class PostgresConnectionString
{
    /// <summary>
    /// Looks up, in order: <c>DATABASE_URL</c>, <c>ConnectionStrings:Postgres</c>,
    /// <c>ConnectionStrings:DefaultConnection</c>. Throws when nothing is configured.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        var candidate =
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("DATABASE_URL"),
                configuration["DATABASE_URL"],
                configuration.GetConnectionString("Postgres"),
                configuration.GetConnectionString("DefaultConnection"))
            ?? throw new InvalidOperationException(
                "No PostgreSQL connection configured. Set the DATABASE_URL environment variable " +
                "or ConnectionStrings:Postgres in configuration.");

        return Normalize(candidate);
    }

    /// <summary>Converts a postgres:// URL to an Npgsql connection string; passes key/value strings through.</summary>
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');
        var port = uri.Port > 0 ? uri.Port : 5432;
        var isLocal = uri.Host is "localhost" or "127.0.0.1";

        var parts = new List<string>
        {
            $"Host={uri.Host}",
            $"Port={port}",
            $"Database={database}",
            $"Username={user}",
            $"Password={password}",
            "Pooling=true",
            "Timeout=30",
            "Command Timeout=60",
        };

        // Managed Postgres requires TLS; local development usually has none.
        parts.Add(isLocal ? "SSL Mode=Disable" : "SSL Mode=Require");
        if (!isLocal) parts.Add("Trust Server Certificate=true");

        return string.Join(";", parts) + ";";
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
