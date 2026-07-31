namespace DaftechCrm.Application.Interfaces;

public record EmailSendResult(bool Success, string? ErrorMessage);

/// <summary>
/// Sends transactional email (account credentials, notifications). The
/// concrete implementation (Infrastructure/Email/MailKitEmailSender) uses
/// MailKit over SMTP per SRS v2.0 §4.3.1. Application-layer services never
/// touch MailKit types directly, so this could be swapped for another
/// provider without touching business logic.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct = default);
}
