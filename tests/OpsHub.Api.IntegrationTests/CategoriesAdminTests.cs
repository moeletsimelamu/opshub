using System.Net;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Categories;
using OpsHub.Api.Features.Tickets;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>Administrators add, rename, deactivate and reactivate ticket categories.</summary>
public class CategoriesAdminTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static readonly string Thandi = DemoData.EmailFor("thandi");   // Requester
    private static readonly string Sipho = DemoData.EmailFor("sipho");     // Support Agent
    private static readonly string Lerato = DemoData.EmailFor("lerato");   // Administrator

    private const string Admin = "/api/admin/categories";

    [Theory]
    [InlineData("thandi")]
    [InlineData("sipho")]
    public async Task OnlyAdministrators_CanManageCategories(string userKey)
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor(userKey));

        using var list = await client.GetPathAsync(Admin);
        using var create = await client.PostJsonAsync(Admin, new CreateCategoryRequest { Name = $"Not allowed {Guid.NewGuid():N}"[..20] });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task SignedOut_Returns401()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync(Admin);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ShowsEveryCategory_WithHowManyTicketsUseIt()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);

        var categories = await ListAsync(lerato);

        Assert.Superset(ReferenceDataSeeder.DefaultCategories.ToHashSet(), categories.Select(c => c.Name).ToHashSet());
        var network = categories.Single(c => c.Name == "Network");
        var networkTickets = await factory.WithDbAsync((db, _) => Task.FromResult(db.Tickets.Count(t => t.CategoryId == network.Id)));
        Assert.Equal(networkTickets, network.TicketCount);
        Assert.True(network.TicketCount > 0);
    }

    [Fact]
    public async Task Create_AddsATrimmedActiveCategory_ThatTheTicketFormsOffer()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var name = UniqueName("Printing");

        using var response = await lerato.PostJsonAsync(Admin, new CreateCategoryRequest { Name = $"  {name}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync<CategoryAdminResponse>();
        Assert.Equal((name, true, 0), (created.Name, created.IsActive, created.TicketCount));
        Assert.Contains(await FormCategoriesAsync(thandi), c => c.Id == created.Id);
    }

    [Theory]
    [InlineData("network")]       // an existing name in a different case
    [InlineData("NETWORK")]
    public async Task Create_WithAnExistingNameIgnoringCase_Returns409(string name)
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);

        using var response = await lerato.PostJsonAsync(Admin, new CreateCategoryRequest { Name = name });

        var problem = await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("category_name_in_use", problem.Code());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutAName_Returns400(string name)
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);

        using var response = await lerato.PostJsonAsync(Admin, new CreateCategoryRequest { Name = name });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithATooLongName_Returns400()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);

        using var response = await lerato.PostJsonAsync(Admin, new CreateCategoryRequest { Name = new string('x', Category.NameMaxLength + 1) });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rename_ShowsTheNewName_OnTicketsAlreadyFiledUnderIt()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var category = await CreateAsync(lerato, UniqueName("Phones"));
        var ticket = await CreateTicketInAsync(thandi, category.Id);
        var newName = UniqueName("Mobile phones");

        using var rename = await lerato.PutJsonAsync($"{Admin}/{category.Id}", new UpdateCategoryRequest { Name = newName, IsActive = true });

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(newName, (await thandi.GetTicketAsync(ticket.Id)).Category.Name);
    }

    [Fact]
    public async Task Rename_ChangingOnlyTheCase_IsAllowed_ButTakingAnotherName_Returns409()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);
        var name = UniqueName("projectors");
        var category = await CreateAsync(lerato, name);

        using var sameNameNewCase = await lerato.PutJsonAsync($"{Admin}/{category.Id}", new UpdateCategoryRequest { Name = name.ToUpperInvariant(), IsActive = true });
        using var anotherName = await lerato.PutJsonAsync($"{Admin}/{category.Id}", new UpdateCategoryRequest { Name = "Hardware", IsActive = true });

        Assert.Equal(HttpStatusCode.OK, sameNameNewCase.StatusCode);
        Assert.Equal("category_name_in_use", (await anotherName.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task Deactivating_HidesItFromTheForms_ButExistingTicketsKeepIt()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var category = await CreateAsync(lerato, UniqueName("Scanners"));
        var ticket = await CreateTicketInAsync(thandi, category.Id);

        using var deactivate = await lerato.PutJsonAsync($"{Admin}/{category.Id}", new UpdateCategoryRequest { Name = category.Name, IsActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        Assert.DoesNotContain(await FormCategoriesAsync(thandi), c => c.Id == category.Id);
        Assert.Equal(category.Name, (await thandi.GetTicketAsync(ticket.Id)).Category.Name);
        using var newTicket = await thandi.PostJsonAsync("/api/tickets", NewTicket(category.Id));
        await newTicket.AssertProblemAsync(HttpStatusCode.BadRequest);

        using var reactivate = await lerato.PutJsonAsync($"{Admin}/{category.Id}", new UpdateCategoryRequest { Name = category.Name, IsActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Contains(await FormCategoriesAsync(thandi), c => c.Id == category.Id);
    }

    [Fact]
    public async Task Update_AnUnknownCategory_Returns404()
    {
        using var lerato = await factory.SignedInClientAsync(Lerato);

        using var response = await lerato.PutJsonAsync($"{Admin}/999999", new UpdateCategoryRequest { Name = "Nothing", IsActive = true });

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheLastActiveCategory_CannotBeDeactivated()
    {
        // A separate, empty installation: this test deactivates categories.
        using var api = new OpsHubApiFactory();
        var admin = await api.CreateUserAsync(AppRoles.Administrator);
        using var client = await api.SignedInClientAsync(admin.Email);
        var categories = await ListAsync(client);

        foreach (var category in categories.Skip(1))
        {
            using var response = await client.PutJsonAsync($"{Admin}/{category.Id}", new UpdateCategoryRequest { Name = category.Name, IsActive = false });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var last = categories[0];
        using var lastOne = await client.PutJsonAsync($"{Admin}/{last.Id}", new UpdateCategoryRequest { Name = last.Name, IsActive = false });

        Assert.Equal("last_active_category", (await lastOne.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static async Task<List<CategoryAdminResponse>> ListAsync(HttpClient client)
    {
        using var response = await client.GetPathAsync(Admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<List<CategoryAdminResponse>>();
    }

    private static async Task<CategoryAdminResponse> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostJsonAsync(Admin, new CreateCategoryRequest { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<CategoryAdminResponse>();
    }

    private static async Task<List<CategorySummary>> FormCategoriesAsync(HttpClient client)
    {
        using var response = await client.GetPathAsync("/api/categories");
        return await response.ReadJsonAsync<List<CategorySummary>>();
    }

    private static CreateTicketRequest NewTicket(int categoryId) => new()
    {
        Title = $"Test ticket {Guid.NewGuid():N}",
        Description = "Created by an integration test.",
        CategoryId = categoryId,
        Priority = TicketPriority.Medium,
    };

    private static async Task<TicketDetails> CreateTicketInAsync(HttpClient client, int categoryId)
    {
        using var response = await client.PostJsonAsync("/api/tickets", NewTicket(categoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetails>();
    }
}
