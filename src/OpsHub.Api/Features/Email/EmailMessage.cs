namespace OpsHub.Api.Features.Email;

/// <summary>
/// A plain-text email. Kind and Reference identify it in logs, which never include
/// addresses or content.
/// </summary>
public sealed record EmailMessage(string To, string ToName, string Subject, string Body, string Kind, string? Reference = null);

/// <summary>Sends one email. SMTP in production, a folder of .eml files in development, a fake in tests.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
