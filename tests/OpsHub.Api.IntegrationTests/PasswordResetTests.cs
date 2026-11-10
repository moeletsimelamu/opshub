using System.Net;
using System.Text.RegularExpressions;
using OpsHub.Api.Features.Auth;
using OpsHub.Api.Features.Email;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>"Forgot your password?": a reset link by email, used once, within an hour.</summary>
public partial class PasswordResetTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private const string NewPassword = "a-brand-new-password-42";

    [Fact]
    public async Task Options_OfferPasswordReset_WhenEmailIsSetUp()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync("/api/auth/options");

        Assert.True((await response.ReadJsonAsync<AuthOptionsResponse>()).PasswordReset);
    }

    [Fact]
    public async Task ForgotPassword_EmailsALinkWithTheUserIdAndAToken()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);

        using var response = await RequestResetAsync(user.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var email = await factory.Mail.WaitForAsync(m => m.To == user.Email && m.Kind == "password-reset");
        var link = ResetLink(email);
        Assert.StartsWith($"{EmailApiFactory.BaseUrl}/reset-password?user={user.Id}&token=", link.Url, StringComparison.Ordinal);
        Assert.DoesNotContain(user.Email, link.Url, StringComparison.OrdinalIgnoreCase); // no personal data in the link
    }

    [Fact]
    public async Task ForgotPassword_ForAnUnknownOrDeactivatedAccount_LooksTheSame_ButSendsNothing()
    {
        var deactivated = await factory.CreateUserAsync(AppRoles.Requester, isActive: false);
        var marker = await factory.CreateUserAsync(AppRoles.Requester);

        using var unknown = await RequestResetAsync("nobody-here@tests.example");
        using var inactive = await RequestResetAsync(deactivated.Email);
        using var known = await RequestResetAsync(marker.Email);

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);

        // Emails go out in order: once the third one has been sent, the first two would have been too.
        await factory.Mail.WaitForAsync(m => m.To == marker.Email);
        Assert.Empty(factory.Mail.To("nobody-here@tests.example"));
        Assert.Empty(factory.Mail.To(deactivated.Email));
    }

    [Fact]
    public async Task ResetPassword_SetsTheNewPassword_EndsOtherSessions_AndTheLinkWorksOnce()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var otherSession = await factory.SignedInClientAsync(user.Email);
        var link = await RequestLinkAsync(user.Email);

        using var reset = await ResetAsync(link, NewPassword);
        using var again = await ResetAsync(link, "yet-another-password-99");

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal("invalid_reset_link", (await again.AssertProblemAsync(HttpStatusCode.BadRequest)).Code());

        using var oldSession = await otherSession.GetPathAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);

        using var withOldPassword = await factory.CreateBrowserClient().LoginAsync(user.Email, factory.DemoPassword);
        using var withNewPassword = await factory.CreateBrowserClient().LoginAsync(user.Email, NewPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, withOldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNewPassword.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_LiftsALockout()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var browser = factory.CreateBrowserClient();
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.LoginAsync(user.Email, "wrong-password-123");
        }

        using var locked = await browser.LoginAsync(user.Email, factory.DemoPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode); // locked out, right password or not

        using var reset = await ResetAsync(await RequestLinkAsync(user.Email), NewPassword);
        using var signIn = await factory.CreateBrowserClient().LoginAsync(user.Email, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAForgedToken_Returns400()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);

        using var response = await ResetAsync((user.Id, "not-a-real-token"), NewPassword);

        Assert.Equal("invalid_reset_link", (await response.AssertProblemAsync(HttpStatusCode.BadRequest)).Code());
    }

    [Fact]
    public async Task ResetPassword_WithATooShortPassword_ExplainsThePasswordRule()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        var link = await RequestLinkAsync(user.Email);

        using var response = await ResetAsync(link, "short");

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("newPassword", problem.Errors.Keys);
    }

    [Fact]
    public async Task ResetEmail_IsPlainTextAndContainsTheLink()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        await RequestResetAsync(user.Email);

        var email = await factory.Mail.WaitForAsync(m => m.To == user.Email);

        Assert.Equal("Reset your OpsHub password", email.Subject);
        Assert.Contains("works once, for one hour", email.Body, StringComparison.Ordinal);
        Assert.Contains(ResetLink(email).Url, email.Body, StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> RequestResetAsync(string email)
    {
        using var client = factory.CreateBrowserClient();
        using var token = await client.GetPathAsync("/api/auth/antiforgery");
        return await client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
    }

    private async Task<(Guid UserId, string Token)> RequestLinkAsync(string email)
    {
        var before = factory.Mail.To(email).Count;
        using var response = await RequestResetAsync(email);
        var message = await factory.Mail.WaitForAsync(m => m.To == email && factory.Mail.To(email).Count > before);
        var link = ResetLink(factory.Mail.To(email)[^1]);
        return (link.UserId, link.Token);
    }

    private async Task<HttpResponseMessage> ResetAsync((Guid UserId, string Token) link, string password)
    {
        using var client = factory.CreateBrowserClient();
        using var token = await client.GetPathAsync("/api/auth/antiforgery");
        return await client.PostJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { UserId = link.UserId, Token = link.Token, NewPassword = password });
    }

    private static (string Url, Guid UserId, string Token) ResetLink(EmailMessage email)
    {
        var match = LinkPattern().Match(email.Body);
        Assert.True(match.Success, "The email contains no reset link.");
        return (match.Value, Guid.Parse(match.Groups["user"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    [GeneratedRegex(@"https://\S+/reset-password\?user=(?<user>[0-9a-f-]{36})&token=(?<token>\S+)")]
    private static partial Regex LinkPattern();
}

/// <summary>Without email, the sign-in page doesn't offer a reset and the endpoint says why.</summary>
public class PasswordResetWithoutEmailTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task WithoutEmail_ResetIsNotOffered_AndExplainsWhom_ToAsk()
    {
        using var client = factory.CreateBrowserClient();
        using var options = await client.GetPathAsync("/api/auth/options");
        using var token = await client.GetPathAsync("/api/auth/antiforgery");

        using var response = await client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest { Email = "someone@tests.example" });

        Assert.False((await options.ReadJsonAsync<AuthOptionsResponse>()).PasswordReset);
        Assert.Equal("password_reset_unavailable", (await response.AssertProblemAsync(HttpStatusCode.NotFound)).Code());
    }
}
