using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace OpsHub.Api.Features.Email;

/// <summary>
/// Sends with .NET's built-in SMTP client: enough for plain-text mail through a mail provider's
/// SMTP relay with STARTTLS, and it can write .eml files to a folder for development.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var client = new SmtpClient();
        if (!string.IsNullOrWhiteSpace(settings.PickupDirectory))
        {
            // A relative folder is inside the API project (its content root), wherever it's started from.
            var folder = Path.GetFullPath(settings.PickupDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(folder);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = folder;
        }
        else
        {
            client.Host = settings.Smtp.Host!; // checked at startup when email is on
            client.Port = settings.Smtp.Port;
            client.EnableSsl = settings.Smtp.UseTls;
            if (!string.IsNullOrWhiteSpace(settings.Smtp.Username))
            {
                client.Credentials = new NetworkCredential(settings.Smtp.Username, settings.Smtp.Password);
            }
        }

        using var mail = new MailMessage(new MailAddress(settings.From!, settings.FromName), new MailAddress(message.To, message.ToName))
        {
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false, // plain text: nothing a ticket title or reply contains can turn into markup
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
        };

        await client.SendMailAsync(mail, cancellationToken);
    }
}
