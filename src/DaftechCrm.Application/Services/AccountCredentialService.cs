using System.Security.Cryptography;
using System.Text;
using DaftechCrm.Application.Interfaces;

namespace DaftechCrm.Application.Services;

public record IssuedCredentials(string Username, string OneTimePassword);

/// <summary>
/// Generates the login username and one-time password for a newly
/// registered account (staff or client), and delivers them by email via
/// IEmailSender — SRS v2.0 §4.3.1 / §3.2 "Account Provisioning &amp;
/// Credential Service". Usernames are initials + random digits (e.g.
/// "mf4821"); collisions against existing Employee/Client usernames are
/// retried with a fresh digit suffix.
/// </summary>
public class AccountCredentialService
{
    private readonly IAppDbContext _db;
    private readonly IEmailSender _email;

    public AccountCredentialService(IAppDbContext db, IEmailSender email)
    {
        _db = db;
        _email = email;
    }

    public async Task<IssuedCredentials> IssueForNameAsync(string fullName, CancellationToken ct = default)
    {
        var initials = ExtractInitials(fullName);
        string username;
        var attempts = 0;

        do
        {
            var digits = RandomNumberGenerator.GetInt32(1000, 9999);
            username = $"{initials}{digits}";
            attempts++;
            if (attempts > 25)
                throw new InvalidOperationException("Could not generate a unique username after 25 attempts.");
        }
        while (await UsernameExistsAsync(username, ct));

        var oneTimePassword = GenerateOneTimePassword();
        return new IssuedCredentials(username, oneTimePassword);
    }

    /// <summary>
    /// Sends the plaintext username/OTP to the person's email. Returns
    /// whether it actually sent — per SRS v2.0 §4.3.1, a failure doesn't
    /// block registration; the caller still has the plaintext to show the
    /// Admin on-screen as a fallback, and the Admin can retry later.
    /// </summary>
    public async Task<(bool Sent, string? Error)> SendCredentialEmailAsync(
        string recipientEmail, string recipientName, string username, string oneTimePassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return (false, "No email address on file.");

        var subject = "Your DAFTECH CRM account";
        var html = $@"
            <p>Hello {System.Net.WebUtility.HtmlEncode(recipientName)},</p>
            <p>An account has been created for you on the DAFTECH CRM system.</p>
            <p><b>Username:</b> {System.Net.WebUtility.HtmlEncode(username)}<br/>
               <b>Temporary password:</b> {System.Net.WebUtility.HtmlEncode(oneTimePassword)}</p>
            <p>This password is single-use — you'll be asked to set a new one the first time you log in.
               Please keep it private and do not forward this email.</p>
            <p>&mdash; DAFTECH</p>";

        var result = await _email.SendAsync(recipientEmail, recipientName, subject, html, ct);
        return (result.Success, result.ErrorMessage);
    }

    /// <summary>Generates a fresh one-time password without touching the username — used when resending a failed credential email.</summary>
    public Task<string> RegenerateOneTimePasswordAsync(CancellationToken ct = default) =>
        Task.FromResult(GenerateOneTimePassword());

    private async Task<bool> UsernameExistsAsync(string username, CancellationToken ct)
    {
        var employeeTaken = _db.Employees.Any(e => e.Username == username);
        var clientTaken = _db.Clients.Any(c => c.Username == username);
        // Both queries are cheap in-memory-scale lookups for this app's data volume;
        // no need for a separate async round trip per check.
        return await Task.FromResult(employeeTaken || clientTaken);
    }

    private static string ExtractInitials(string fullName)
    {
        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        var initials = parts.Count switch
        {
            0 => "u",
            1 => parts[0][..Math.Min(2, parts[0].Length)],
            _ => $"{parts[0][0]}{parts[^1][0]}",
        };

        return initials.ToLowerInvariant();
    }

    /// <summary>
    /// A readable one-time password: avoids visually ambiguous characters
    /// (0/O, 1/l/I) since the Admin has to relay this to the person by
    /// voice, chat, or a written note.
    /// </summary>
    private static string GenerateOneTimePassword()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var sb = new StringBuilder();
        for (var i = 0; i < 10; i++)
        {
            sb.Append(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
        }
        return sb.ToString();
    }
}
