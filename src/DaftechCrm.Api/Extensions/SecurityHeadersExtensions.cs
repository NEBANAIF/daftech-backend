namespace DaftechCrm.Api.Extensions;

/// <summary>
/// HTTPS enforcement plus the OWASP-recommended response headers. Registered
/// early in the pipeline so every response — including error responses — carries them.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>Adds HSTS configuration (production only has any effect).</summary>
    public static IServiceCollection AddSecurityHardening(this IServiceCollection services)
    {
        services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });

        services.AddHttpsRedirection(options => options.HttpsPort = 443);

        return services;
    }

    /// <summary>Applies HTTPS redirection, HSTS (production) and security headers.</summary>
    public static IApplicationBuilder UseSecurityHardening(this WebApplication app)
    {
        // Platforms like Render terminate TLS at their edge and forward plain HTTP
        // internally (including health-check probes). Redirecting there produces 307
        // loops and failed health checks, so it is opt-out via configuration/env.
        var behindProxy = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT"));
        var enforceHttps = app.Configuration.GetValue("Security:EnforceHttpsRedirection", !behindProxy);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            if (enforceHttps) app.UseHttpsRedirection();
        }


        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["X-XSS-Protection"] = "1; mode=block";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.Remove("X-Powered-By");
            headers.Remove("Server");

            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                // Swagger UI needs inline scripts/styles; the API surface itself does not.
                headers["Content-Security-Policy"] =
                    "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; img-src 'self' data:; connect-src 'self'";
            }

            await next();
        });

        return app;
    }
}
