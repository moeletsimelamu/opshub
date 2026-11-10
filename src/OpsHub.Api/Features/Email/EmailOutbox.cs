using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace OpsHub.Api.Features.Email;

/// <summary>
/// Emails wait here and are sent in the background, so a slow or unavailable mail server never
/// slows down or fails the request that caused them (a reply, a password reset).
/// </summary>
public sealed partial class EmailOutbox(IOptions<EmailOptions> options, ILogger<EmailOutbox> logger)
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public bool Enabled => options.Value.Enabled;

    /// <summary>Queues the email if email is switched on; otherwise does nothing.</summary>
    public void Enqueue(EmailMessage message)
    {
        if (!Enabled)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(message))
        {
            LogDropped(logger, message.Kind, message.Reference);
        }
    }

    internal ChannelReader<EmailMessage> Reader => _channel.Reader;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email queue full: dropped a {Kind} email ({Reference})")]
    private static partial void LogDropped(ILogger logger, string kind, string? reference);
}

/// <summary>Sends queued emails one at a time, retrying a failed one twice before giving up.</summary>
internal sealed partial class EmailDispatcher(EmailOutbox queue, IEmailSender sender, ILogger<EmailDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await sender.SendAsync(message, stoppingToken);
                    LogSent(logger, message.Kind, message.Reference);
                    break;
                }
                catch (Exception e) when (e is SmtpException or IOException or InvalidOperationException or UnauthorizedAccessException)
                {
                    if (attempt >= RetryDelays.Length)
                    {
                        // The type and SMTP status only: a server's error text can contain the address.
                        LogFailed(logger, message.Kind, message.Reference, e.GetType().Name, (e as SmtpException)?.StatusCode.ToString() ?? "-");
                        break;
                    }

                    await Task.Delay(RetryDelays[attempt], stoppingToken);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent a {Kind} email ({Reference})")]
    private static partial void LogSent(ILogger logger, string kind, string? reference);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not send a {Kind} email ({Reference}) after 3 attempts: {Error} (SMTP status {SmtpStatus})")]
    private static partial void LogFailed(ILogger logger, string kind, string? reference, string error, string smtpStatus);
}
