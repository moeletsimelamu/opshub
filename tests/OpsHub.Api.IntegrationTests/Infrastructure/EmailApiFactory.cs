using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Features.Email;

namespace OpsHub.Api.IntegrationTests.Infrastructure;

/// <summary>Records emails instead of sending them. Emails are sent in the background, so tests wait for them.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Waits up to five seconds for an email matching the condition.</summary>
    public async Task<EmailMessage> WaitForAsync(Func<EmailMessage, bool> condition)
    {
        for (var waited = 0; waited < 5000; waited += 50)
        {
            if (_sent.FirstOrDefault(condition) is { } message)
            {
                return message;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new Xunit.Sdk.XunitException("No matching email was sent within five seconds.");
    }

    public IReadOnlyList<EmailMessage> To(string address) => _sent.Where(m => m.To == address).ToList();
}

/// <summary>The API with demo data and email switched on, delivering to a <see cref="FakeEmailSender"/>.</summary>
public sealed class EmailApiFactory : OpsHubApiFactory
{
    public const string BaseUrl = "https://opshub.tests.example";

    public FakeEmailSender Mail { get; } = new();

    protected override bool SeedDemoData => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Email:Enabled", "true");
        builder.UseSetting("Email:From", "opshub@tests.example");
        builder.UseSetting("Email:PublicBaseUrl", BaseUrl);
        builder.UseSetting("Email:Smtp:Host", "smtp.tests.example"); // never contacted: the fake replaces the sender
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Mail));
    }
}
