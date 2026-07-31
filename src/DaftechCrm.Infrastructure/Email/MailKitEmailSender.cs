using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;
using Polly.Retry;

namespace DaftechCrm.Infrastructure.Email;

/// <summary>
/// SRS v2.0 §4.3.1 / §3.2 Account Provisioning &amp; Credential Service: sends
/// account-credential and notification emails over SMTP via MailKit, wrapped in a
/// Polly retry policy (3 attempts, exponential backoff) so a single transient SMTP
/// hiccup no longer loses the email. If every attempt fails the caller
/// (AccountCredentialService) records the failure so an Admin can retry or fall back
/// to the on-screen reveal — per NFR-9 credentials are still only ever shown once.
/// </summary>
public class MailKitEmailSender : IEmailSender
{
    private const int MaxRetryAttempts = 3;

    private readonly SmtpOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public MailKitEmailSender(IOptions<SmtpOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<SmtpCommandException>(IsTransient)
            .Or<SmtpProtocolException>()
            .Or<IOException>()
            .Or<TimeoutException>()
            .Or<System.Net.Sockets.SocketException>()
            .WaitAndRetryAsync(
                MaxRetryAttempts,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
                (exception, delay, attempt, _) => _logger.LogWarning(
                    exception,
                    "Transient SMTP failure on attempt {Attempt}/{MaxAttempts}; retrying in {DelaySeconds}s",
                    attempt, MaxRetryAttempts, delay.TotalSeconds));
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async token => await SendOnceAsync(toAddress, toName, subject, htmlBody, token), ct);
            _logger.LogInformation("Email '{Subject}' sent to {ToAddress}", subject, toAddress);
            return new EmailSendResult(true, null);
        }
        catch (AuthenticationException ex)
        {
            // Never retried and never logged with credentials attached.
            _logger.LogError(ex, "SMTP authentication failed for host {Host}", _options.Host);
            return new EmailSendResult(false, "SMTP authentication failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress} after {MaxAttempts} attempts", toAddress, MaxRetryAttempts + 1);
            return new EmailSendResult(false, ex.Message);
        }
    }

    /// <summary>Verifies the SMTP endpoint is reachable — used by the email health check.</summary>
    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = CreateClient();
            await client.ConnectAsync(_options.Host, _options.Port, SocketOptions, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP health probe failed for {Host}:{Port}", _options.Host, _options.Port);
            return false;
        }
    }

    private async Task SendOnceAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = CreateClient();
        await client.ConnectAsync(_options.Host, _options.Port, SocketOptions, ct);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private SecureSocketOptions SocketOptions =>
        _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

    private SmtpClient CreateClient() => new() { Timeout = 30_000 }; // 30s per attempt

    /// <summary>4xx SMTP codes are permanent (bad mailbox); 5xx/service codes are worth retrying.</summary>
    private static bool IsTransient(SmtpCommandException ex) =>
        ex.StatusCode is SmtpStatusCode.ServiceNotAvailable
            or SmtpStatusCode.MailboxBusy
            or SmtpStatusCode.TransactionFailed
            or SmtpStatusCode.InsufficientStorage;
}
