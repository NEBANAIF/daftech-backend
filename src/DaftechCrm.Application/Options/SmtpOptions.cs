namespace DaftechCrm.Application.Options;

/// <summary>Bound from appsettings.json ("Smtp" section).</summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.example.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FromAddress { get; set; } = "no-reply@daftech.et";
    public string FromName { get; set; } = "DAFTECH CRM";
}
