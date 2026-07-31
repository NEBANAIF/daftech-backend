namespace DaftechCrm.Application.Options;

/// <summary>SRS v2.0 §4.8 / NFR-10: session/presence tracking thresholds.</summary>
public class SessionOptions
{
    public const string SectionName = "Session";

    /// <summary>Minutes of no activity ping before a session is marked offline.</summary>
    public int OfflineAfterMinutes { get; set; } = 5;
}
