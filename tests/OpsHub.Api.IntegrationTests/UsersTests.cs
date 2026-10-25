using System.Net;
using OpsHub.Api.Common;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Auth;
using OpsHub.Api.Features.Users;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

public class UsersTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static readonly string AdminEmail = DemoData.EmailFor("lerato");

    // ---------- Who may use the user administration API ----------

    [Fact]
    public async Task ListUsers_Anonymous_Returns401()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync("/api/users");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("thandi")]   // Requester
    [InlineData("sipho")]    // Support Agent
    public async Task ListUsers_NonAdministrator_Returns403(string userKey)
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor(userKey));

        using var response = await client.GetPathAsync("/api/users");

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_AsRequester_Returns403_SoNobodyCanGrantThemselvesAccess()
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor("pieter"));

        using var response = await client.PostJsonAsync("/api/users", NewUser(AppRoles.Administrator));

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- Listing, search, filters, paging ----------

    [Fact]
    public async Task ListUsers_AsAdministrator_ReturnsPagedUsers()
    {
        using var client = await factory.SignedInClientAsync(AdminEmail);

        using var response = await client.GetPathAsync("/api/users?page=1&pageSize=5");

        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>();
        Assert.Equal(5, page.Items.Count);
        Assert.True(page.TotalCount >= DemoData.Users.Length);
        Assert.Equal((int)Math.Ceiling(page.TotalCount / 5.0), page.TotalPages);
    }

    [Fact]
    public async Task ListUsers_SearchIsCaseInsensitive_AndMatchesNameOrEmail()
    {
        using var client = await factory.SignedInClientAsync(AdminEmail);

        using var byName = await client.GetPathAsync("/api/users?search=NDLOVU");
        using var byEmail = await client.GetPathAsync("/api/users?search=sipho@marula");

        Assert.Equal("Sipho Ndlovu", Assert.Single((await byName.ReadJsonAsync<PagedResult<UserResponse>>()).Items).DisplayName);
        Assert.Equal("Sipho Ndlovu", Assert.Single((await byEmail.ReadJsonAsync<PagedResult<UserResponse>>()).Items).DisplayName);
    }

    [Fact]
    public async Task ListUsers_SearchTreatsPercentSignLiterally()
    {
        using var client = await factory.SignedInClientAsync(AdminEmail);

        // Unescaped, "%" would be a LIKE wildcard and match every user.
        using var response = await client.GetPathAsync("/api/users?search=%25");

        Assert.Empty((await response.ReadJsonAsync<PagedResult<UserResponse>>()).Items);
    }

    [Fact]
    public async Task ListUsers_FilterByRoleAndActive_ReturnsOnlyMatchingUsers()
    {
        using var client = await factory.SignedInClientAsync(AdminEmail);

        using var response = await client.GetPathAsync("/api/users?role=SupportAgent&isActive=false");

        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>();
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, u =>
        {
            Assert.Equal(AppRoles.SupportAgent, u.Role);
            Assert.False(u.IsActive);
        });
    }

    [Theory]
    [InlineData("/api/users?pageSize=101")]
    [InlineData("/api/users?page=0")]
    [InlineData("/api/users?role=SuperUser")]
    public async Task ListUsers_InvalidQuery_Returns400(string url)
    {
        using var client = await factory.SignedInClientAsync(AdminEmail);

        using var response = await client.GetPathAsync(url);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    // ---------- Creating users ----------

    [Fact]
    public async Task CreateUser_AsAdministrator_Returns201_AndTheUserCanSignIn()
    {
        using var admin = await factory.SignedInClientAsync(AdminEmail);
        var request = NewUser(AppRoles.SupportAgent);

        using var response = await admin.PostJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync<UserResponse>();
        Assert.Equal($"/api/users/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(AppRoles.SupportAgent, created.Role);
        Assert.True(created.IsActive);

        using var newUser = factory.CreateBrowserClient();
        using var login = await newUser.LoginAsync(request.Email, request.Password);
        Assert.Equal(AppRoles.SupportAgent, (await login.ReadJsonAsync<CurrentUserResponse>()).Role);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var response = await admin.PostJsonAsync("/api/users", NewUser(AppRoles.Requester) with { Email = DemoData.EmailFor("emma") });

        var problem = await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("email_in_use", problem.Code());
    }

    [Fact]
    public async Task CreateUser_WeakPassword_Returns400OnPasswordField()
    {
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var response = await admin.PostJsonAsync("/api/users", NewUser(AppRoles.Requester) with { Password = "short" });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("password", problem.Errors.Keys);
    }

    [Fact]
    public async Task CreateUser_UnknownRole_Returns400OnRoleField()
    {
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var response = await admin.PostJsonAsync("/api/users", NewUser("SuperUser"));

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("role", problem.Errors.Keys);
    }

    // ---------- Updating users ----------

    [Fact]
    public async Task UpdateUser_AdministratorCannotDeactivateThemselves()
    {
        var admin = await factory.CreateUserAsync(AppRoles.Administrator);
        using var client = await factory.SignedInClientAsync(admin.Email);

        using var response = await client.PutJsonAsync($"/api/users/{admin.Id}",
            new UpdateUserRequest { DisplayName = "Test Administrator", Role = AppRoles.Administrator, IsActive = false });

        Assert.Equal("cannot_deactivate_self", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task UpdateUser_AdministratorCannotRemoveTheirOwnAdminRole()
    {
        var admin = await factory.CreateUserAsync(AppRoles.Administrator);
        using var client = await factory.SignedInClientAsync(admin.Email);

        using var response = await client.PutJsonAsync($"/api/users/{admin.Id}",
            new UpdateUserRequest { DisplayName = "Test Administrator", Role = AppRoles.Requester, IsActive = true });

        Assert.Equal("cannot_demote_self", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task UpdateUser_Deactivation_EndsExistingSession_AndBlocksSignIn()
    {
        var agent = await factory.CreateUserAsync(AppRoles.SupportAgent);
        using var agentBrowser = await factory.SignedInClientAsync(agent.Email);
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var update = await admin.PutJsonAsync($"/api/users/{agent.Id}",
            new UpdateUserRequest { DisplayName = "Test SupportAgent", Role = AppRoles.SupportAgent, IsActive = false });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.False((await update.ReadJsonAsync<UserResponse>()).IsActive);

        using var me = await agentBrowser.GetPathAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        using var fresh = factory.CreateBrowserClient();
        using var login = await fresh.LoginAsync(agent.Email, factory.DemoPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_RoleChange_EndsOldSession_AndNewSessionHasNewRole()
    {
        var requester = await factory.CreateUserAsync(AppRoles.Requester);
        using var oldSession = await factory.SignedInClientAsync(requester.Email);
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var update = await admin.PutJsonAsync($"/api/users/{requester.Id}",
            new UpdateUserRequest { DisplayName = "Promoted Agent", Role = AppRoles.SupportAgent, IsActive = true });
        Assert.Equal(AppRoles.SupportAgent, (await update.ReadJsonAsync<UserResponse>()).Role);

        using var oldMe = await oldSession.GetPathAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, oldMe.StatusCode);

        using var newSession = await factory.SignedInClientAsync(requester.Email);
        using var newMe = await newSession.GetPathAsync("/api/auth/me");
        var me = await newMe.ReadJsonAsync<CurrentUserResponse>();
        Assert.Equal(AppRoles.SupportAgent, me.Role);
        Assert.Equal("Promoted Agent", me.DisplayName);
    }

    [Fact]
    public async Task UpdateUser_DisplayNameOnly_KeepsExistingSession()
    {
        var requester = await factory.CreateUserAsync(AppRoles.Requester);
        using var session = await factory.SignedInClientAsync(requester.Email);
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var update = await admin.PutJsonAsync($"/api/users/{requester.Id}",
            new UpdateUserRequest { DisplayName = "Renamed Requester", Role = AppRoles.Requester, IsActive = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var me = await session.GetPathAsync("/api/auth/me");
        Assert.Equal("Renamed Requester", (await me.ReadJsonAsync<CurrentUserResponse>()).DisplayName);
    }

    [Fact]
    public async Task UpdateUser_UnknownId_Returns404()
    {
        using var admin = await factory.SignedInClientAsync(AdminEmail);

        using var response = await admin.PutJsonAsync($"/api/users/{Guid.NewGuid()}",
            new UpdateUserRequest { DisplayName = "Nobody", Role = AppRoles.Requester, IsActive = true });

        Assert.Equal("user_not_found", (await response.AssertProblemAsync(HttpStatusCode.NotFound)).Code());
    }

    private static CreateUserRequest NewUser(string role) => new()
    {
        Email = $"new-{Guid.NewGuid():N}@tests.example",
        DisplayName = "New Test User",
        Role = role,
        Password = "a-long-test-password",
    };
}
