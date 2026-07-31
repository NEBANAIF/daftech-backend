namespace DaftechCrm.Application.Options;

/// <summary>
/// SRS v2.0 NFR-11: AI narrative summaries are best-effort and optional.
/// If ApiKey is empty, the feature is treated as unconfigured and every
/// call short-circuits to Available=false without attempting a request.
/// </summary>
public class AiReportingOptions
{
    public const string SectionName = "AiReporting";

    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-5";
    public string ApiBaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";
    public int TimeoutSeconds { get; set; } = 20;
}
