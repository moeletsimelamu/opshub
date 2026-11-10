using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using OpsHub.Api.Features.Email;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

public class EmailInfrastructureTests(OpsHubApiFactory baseFactory) : IClassFixture<OpsHubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InDevelopment_EmailsAreWrittenToTheFolder_AsPlainText()
    {
        var folder = Directory.CreateTempSubdirectory("opshub-mail-");
        try
        {
            var sender = new SmtpEmailSender(Options.Create(new EmailOptions
            {
                Enabled = true,
                From = "opshub@tests.example",
                PickupDirectory = folder.FullName,
            }), new HostingEnvironment { ContentRootPath = Path.GetTempPath() });

            await sender.SendAsync(new EmailMessage("thandi@tests.example", "Thandi Mokoena", "Hello", "A plain body.", "test"), Ct);

            var eml = await File.ReadAllTextAsync(Assert.Single(folder.GetFiles("*.eml")).FullName, Ct);
            Assert.Contains("To: \"Thandi Mokoena\" <thandi@tests.example>", eml, StringComparison.Ordinal);
            Assert.Contains("Subject: Hello", eml, StringComparison.Ordinal);
            Assert.Contains("Content-Type: text/plain", eml, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void ATicketTitle_CannotAddLinesToTheSubject()
    {
        var ticket = new TicketContact(12, "Printer broken\r\nBcc: everyone@example.com", new Person(Guid.NewGuid(), "Thandi", "t@tests.example", true), Agent: null);

        var email = EmailTemplates.StatusForRequester(ticket, Domain.Tickets.TicketStatus.Resolved, note: null, "https://opshub.tests.example/tickets/12");

        Assert.DoesNotContain('\n', email.Subject);
        Assert.DoesNotContain('\r', email.Subject);
        Assert.StartsWith("[OPS-000012] Resolved: please confirm the fix: Printer broken", email.Subject, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "https://opshub.tests.example", "smtp.tests.example", "Email:From")]
    [InlineData("opshub@tests.example", "not a url", "smtp.tests.example", "Email:PublicBaseUrl")]
    [InlineData("opshub@tests.example", "https://opshub.tests.example", null, "Email:Smtp:Host")]
    public void EmailSwitchedOnButIncomplete_StopsTheAppFromStarting(string? from, string baseUrl, string? host, string expected)
    {
        using var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Email:Enabled", "true");
            b.UseSetting("Email:From", from);
            b.UseSetting("Email:PublicBaseUrl", baseUrl);
            b.UseSetting("Email:Smtp:Host", host);
        });

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(expected, exception.ToString(), StringComparison.Ordinal);
    }
}
