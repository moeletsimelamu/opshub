using System.Net;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Tickets;
using OpsHub.Api.Features.Users;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>
/// Ticket API behaviour end to end: HTTP → controller → service → workflow → SQLite.
/// Each test creates the tickets it needs, so tests don't depend on each other.
/// </summary>
public class TicketsTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static readonly string Thandi = DemoData.EmailFor("thandi");   // Requester
    private static readonly string Pieter = DemoData.EmailFor("pieter");   // Requester
    private static readonly string Sipho = DemoData.EmailFor("sipho");     // Support Agent
    private static readonly string Lerato = DemoData.EmailFor("lerato");   // Administrator

    // ---------- Creating ----------

    [Fact]
    public async Task Create_AsRequester_Returns201_OwnedByTheCaller_EvenIfTheBodyNamesSomeoneElse()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var pieter = await factory.SignedInClientAsync(Pieter);
        var pieterId = (await pieter.MeAsync()).Id;

        // A malicious client adds requesterId; the API must ignore it.
        using var response = await thandi.PostJsonAsync("/api/tickets", new
        {
            title = "Monitor cable is loose",
            description = "The monitor flickers when the desk moves.",
            categoryId = await thandi.FirstCategoryIdAsync(),
            priority = "Low",
            requesterId = pieterId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ticket = await response.ReadJsonAsync<TicketDetails>();
        Assert.Equal($"/api/tickets/{ticket.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal((await thandi.MeAsync()).Id, ticket.Requester.Id);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Equal(Ticket.FormatReference(ticket.Id), ticket.Reference);
        Assert.Null(ticket.AssignedAgent);

        using var history = await thandi.GetPathAsync($"/api/tickets/{ticket.Id}/history");
        var entry = Assert.Single(await history.ReadJsonAsync<List<StatusHistoryResponse>>());
        Assert.Null(entry.FromStatus);
        Assert.Equal(TicketStatus.Open, entry.ToStatus);
    }

    [Fact]
    public async Task Create_WithInvalidFields_Returns400WithAnErrorPerField()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);

        using var response = await thandi.PostJsonAsync("/api/tickets", new { title = "Hi", description = "", categoryId = 0 });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("title", problem.Errors.Keys);
        Assert.Contains("description", problem.Errors.Keys);
        Assert.Contains("categoryId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_Returns400OnCategory()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);

        using var response = await thandi.PostJsonAsync("/api/tickets",
            new { title = "Valid title", description = "A valid description.", categoryId = 9999 });

        Assert.Contains("categoryId", (await response.AssertProblemAsync(HttpStatusCode.BadRequest)).Errors.Keys);
    }

    [Fact]
    public async Task Create_WithUnknownPriority_Returns400()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);

        using var response = await thandi.PostJsonAsync("/api/tickets", new
        {
            title = "Valid title",
            description = "A valid description.",
            categoryId = await thandi.FirstCategoryIdAsync(),
            priority = "Urgent",
        });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    // ---------- Who can see what ----------

    [Fact]
    public async Task List_AsRequester_ContainsOnlyTheirOwnTickets()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var thandiId = (await thandi.MeAsync()).Id;

        var page = await thandi.ListTicketsAsync("?pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, t => Assert.Equal(thandiId, t.Requester.Id));
    }

    [Fact]
    public async Task List_AsStaff_ContainsEveryonesTickets()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);

        var page = await sipho.ListTicketsAsync("?pageSize=100");

        Assert.True(page.Items.Select(t => t.Requester.Id).Distinct().Count() > 1);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/comments")]
    [InlineData("GET", "/history")]
    [InlineData("POST", "/comments")]
    [InlineData("PUT", "/status")]
    [InlineData("PUT", "")]
    public async Task AnotherRequestersTicket_Returns404OnEveryEndpoint(string method, string suffix)
    {
        using var pieter = await factory.SignedInClientAsync(Pieter);
        var pieterTicket = await pieter.CreateTicketAsync();
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var path = $"/api/tickets/{pieterTicket.Id}{suffix}";

        using var response = method switch
        {
            "GET" => await thandi.GetPathAsync(path),
            "POST" => await thandi.PostJsonAsync(path, new { body = "Can I see this?" }),
            _ when suffix == "/status" => await thandi.PutJsonAsync(path, new { status = "Closed", version = pieterTicket.Version }),
            _ => await thandi.PutJsonAsync(path, new
            {
                title = "Hijacked title", description = "Hijacked description.",
                categoryId = pieterTicket.Category.Id, priority = "Medium", version = pieterTicket.Version,
            }),
        };

        // 404, not 403: the response must not confirm that the ticket exists.
        Assert.Equal("ticket_not_found", (await response.AssertProblemAsync(HttpStatusCode.NotFound)).Code());
    }

    [Fact]
    public async Task Search_CannotFindAnotherRequestersTicket()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        using var pieter = await factory.SignedInClientAsync(Pieter);
        await pieter.CreateTicketAsync(title: $"Scanner fault {token}");
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);

        Assert.Empty((await thandi.ListTicketsAsync($"?search={token}")).Items);
        Assert.Single((await sipho.ListTicketsAsync($"?search={token}")).Items);
    }

    // ---------- Filtering, searching, sorting, paging ----------

    [Fact]
    public async Task List_FiltersByMultipleStatuses()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);

        var page = await sipho.ListTicketsAsync("?status=Open&status=Resolved&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, t => Assert.Contains(t.Status, new[] { TicketStatus.Open, TicketStatus.Resolved }));
    }

    [Fact]
    public async Task List_FiltersUnassignedAndByPriority()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);

        var page = await sipho.ListTicketsAsync("?unassigned=true&priority=Low&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, t =>
        {
            Assert.Null(t.AssignedAgent);
            Assert.Equal(TicketPriority.Low, t.Priority);
        });
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("number")]
    public async Task List_SearchFindsTicketByReferenceOrNumber(string form)
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();
        var term = form == "reference" ? ticket.Reference.ToLowerInvariant() : ticket.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var page = await thandi.ListTicketsAsync($"?search={term}");

        Assert.Contains(page.Items, t => t.Id == ticket.Id);
    }

    [Fact]
    public async Task List_SortsByPriorityDescending()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);

        var page = await sipho.ListTicketsAsync("?sortBy=priority&sortDirection=desc&pageSize=100");

        var priorities = page.Items.Select(t => (int)t.Priority).ToList();
        Assert.Equal(priorities.OrderByDescending(p => p), priorities);
        Assert.Equal(TicketPriority.Critical, page.Items[0].Priority);
    }

    [Fact]
    public async Task List_PagesDoNotOverlap()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);

        var first = await sipho.ListTicketsAsync("?pageSize=5&page=1");
        var second = await sipho.ListTicketsAsync("?pageSize=5&page=2");

        Assert.Equal(5, first.Items.Count);
        Assert.Empty(first.Items.Select(t => t.Id).Intersect(second.Items.Select(t => t.Id)));
        Assert.Equal((int)Math.Ceiling(first.TotalCount / 5.0), first.TotalPages);
    }

    [Theory]
    [InlineData("?sortBy=passwordHash")]
    [InlineData("?sortDirection=sideways")]
    [InlineData("?status=Exploded")]
    [InlineData("?pageSize=500")]
    public async Task List_WithInvalidQuery_Returns400(string query)
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);

        using var response = await sipho.GetPathAsync($"/api/tickets{query}");

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    // ---------- Editing ----------

    [Fact]
    public async Task Update_RequesterCanEditTheirOwnOpenTicket()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();

        var updated = await thandi.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket, title: "Clearer title for the agent"))
            .ExpectTicketAsync();

        Assert.Equal("Clearer title for the agent", updated.Title);
        Assert.NotEqual(ticket.Version, updated.Version);
    }

    [Fact]
    public async Task Update_RequesterCannotChangePriority()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync(priority: TicketPriority.Low);

        using var response = await thandi.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket, priority: TicketPriority.Critical));

        Assert.Equal("priority_change_requires_staff", (await response.AssertProblemAsync(HttpStatusCode.Forbidden)).Code());
    }

    [Fact]
    public async Task Update_RequesterCannotEditOnceWorkHasStarted()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();
        ticket = await sipho.AssignAsync(ticket, (await sipho.MeAsync()).Id).ExpectTicketAsync();
        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.InProgress).ExpectTicketAsync();

        using var response = await thandi.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket, title: "Changed my mind about the title"));

        Assert.Equal("ticket_locked", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task Update_StaffCanChangePriority()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync(priority: TicketPriority.Low);

        var updated = await sipho.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket, priority: TicketPriority.High))
            .ExpectTicketAsync();

        Assert.Equal(TicketPriority.High, updated.Priority);
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409_InsteadOfOverwriting()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();

        // Both load version V1; the agent saves first.
        await sipho.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket, title: "Agent's improved title")).ExpectTicketAsync();
        using var response = await thandi.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket, title: "Requester's late title"));

        Assert.Equal("concurrency_conflict", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
        Assert.Equal("Agent's improved title", (await thandi.GetTicketAsync(ticket.Id)).Title);
    }

    [Fact]
    public async Task Update_WithoutVersion_Returns400()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();

        using var response = await thandi.PutJsonAsync($"/api/tickets/{ticket.Id}", EditOf(ticket) with { Version = null });

        Assert.Contains("version", (await response.AssertProblemAsync(HttpStatusCode.BadRequest)).Errors.Keys);
    }

    // ---------- Assigning ----------

    [Fact]
    public async Task Assign_AsRequester_Returns403()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();

        using var response = await thandi.AssignAsync(ticket, (await thandi.MeAsync()).Id);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("thandi")]  // a requester
    [InlineData("kagiso")]  // a deactivated agent
    [InlineData("lerato")]  // an administrator (assignments go to support agents)
    public async Task Assign_ToSomeoneWhoIsNotAnActiveAgent_Returns400(string userKey)
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);
        using var admin = await factory.SignedInClientAsync(Lerato);
        var ticket = await sipho.CreateTicketAsync();
        var target = Assert.Single((await (await admin.GetPathAsync($"/api/users?search={userKey}@"))
            .ReadJsonAsync<OpsHub.Api.Common.PagedResult<UserResponse>>()).Items);

        using var response = await sipho.AssignAsync(ticket, target.Id);

        Assert.Contains("agentId", (await response.AssertProblemAsync(HttpStatusCode.BadRequest)).Errors.Keys);
    }

    [Fact]
    public async Task Assign_ToActiveAgent_UpdatesTicketAndWorkload()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var siphoId = (await sipho.MeAsync()).Id;
        var ticket = await thandi.CreateTicketAsync();
        var workloadBefore = (await AgentsAsync(sipho)).Single(a => a.Id == siphoId).ActiveTicketCount;

        var assigned = await sipho.AssignAsync(ticket, siphoId).ExpectTicketAsync();

        Assert.Equal(siphoId, assigned.AssignedAgent?.Id);
        var agents = await AgentsAsync(sipho);
        Assert.Equal(workloadBefore + 1, agents.Single(a => a.Id == siphoId).ActiveTicketCount);
        Assert.DoesNotContain(agents, a => a.DisplayName == "Kagiso Molefe"); // deactivated agents aren't offered
    }

    [Fact]
    public async Task Unassign_TicketInProgress_ReturnsItToTheQueue()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();
        ticket = await sipho.AssignAsync(ticket, (await sipho.MeAsync()).Id).ExpectTicketAsync();
        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.InProgress).ExpectTicketAsync();

        var unassigned = await sipho.AssignAsync(ticket, agentId: null).ExpectTicketAsync();

        Assert.Null(unassigned.AssignedAgent);
        Assert.Equal(TicketStatus.Open, unassigned.Status);
    }

    [Fact]
    public async Task AgentsLookup_AsRequester_Returns403()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);

        using var response = await thandi.GetPathAsync("/api/agents");

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- Status workflow ----------

    [Fact]
    public async Task FullLifecycle_FollowsTheWorkflowAndRecordsHistory()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();

        ticket = await sipho.AssignAsync(ticket, (await sipho.MeAsync()).Id).ExpectTicketAsync();
        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.InProgress).ExpectTicketAsync();
        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.WaitingForInformation, "Which floor are you on?").ExpectTicketAsync();

        // The requester answers: the ticket goes back to In Progress automatically.
        using (var reply = await thandi.CommentAsync(ticket.Id, "Second floor, next to the kitchen."))
        {
            Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        }

        ticket = await sipho.GetTicketAsync(ticket.Id);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);

        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.Resolved, "Replaced the cable.").ExpectTicketAsync();
        Assert.NotNull(ticket.ResolvedAt);

        // The requester confirms the fix by closing the ticket.
        Assert.Contains(TicketStatus.Closed, (await thandi.GetTicketAsync(ticket.Id)).Permissions.AllowedStatuses);
        ticket = await thandi.ChangeStatusAsync(ticket, TicketStatus.Closed).ExpectTicketAsync();
        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.NotNull(ticket.ResolvedAt);
        Assert.False(ticket.Permissions.CanComment);

        using var historyResponse = await thandi.GetPathAsync($"/api/tickets/{ticket.Id}/history");
        var path = (await historyResponse.ReadJsonAsync<List<StatusHistoryResponse>>()).Select(h => h.ToStatus);
        Assert.Equal(
            [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingForInformation, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
            path);
    }

    [Fact]
    public async Task ChangeStatus_ToInProgressWithoutAssignee_Returns409()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await sipho.CreateTicketAsync();

        using var response = await sipho.ChangeStatusAsync(ticket, TicketStatus.InProgress);

        Assert.Equal("assignee_required", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task ChangeStatus_UndefinedTransition_Returns409()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await sipho.CreateTicketAsync();
        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.Closed, "Duplicate request.").ExpectTicketAsync();

        using var response = await sipho.ChangeStatusAsync(ticket, TicketStatus.Open);

        Assert.Equal("transition_not_allowed", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task ChangeStatus_RequesterCannotResolveTheirOwnTicket()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();

        using var response = await thandi.ChangeStatusAsync(ticket, TicketStatus.Resolved);

        Assert.Equal("transition_requires_staff", (await response.AssertProblemAsync(HttpStatusCode.Forbidden)).Code());
    }

    [Fact]
    public async Task ChangeStatus_ClosingAnOpenTicketWithoutANote_Returns400OnNote()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await sipho.CreateTicketAsync();

        using var response = await sipho.ChangeStatusAsync(ticket, TicketStatus.Closed);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("note_required", problem.Code());
        Assert.Contains("note", problem.Errors.Keys);
    }

    [Fact]
    public async Task ChangeStatus_RequesterCanReopenAResolvedTicket()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();
        ticket = await sipho.ChangeStatusAsync(ticket, TicketStatus.Resolved, "Restarted the service.").ExpectTicketAsync();

        var reopened = await thandi.ChangeStatusAsync(ticket, TicketStatus.Open, "Still not working.").ExpectTicketAsync();

        Assert.Equal(TicketStatus.Open, reopened.Status);
        Assert.Null(reopened.ResolvedAt);
    }

    // ---------- Comments ----------

    [Fact]
    public async Task InternalComments_AreHiddenFromTheRequester()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();
        (await sipho.CommentAsync(ticket.Id, "Probably the same fault as last week.", isInternal: true)).Dispose();
        (await sipho.CommentAsync(ticket.Id, "I'm looking into this now.")).Dispose();

        using var requesterView = await thandi.GetPathAsync($"/api/tickets/{ticket.Id}/comments");
        using var staffView = await sipho.GetPathAsync($"/api/tickets/{ticket.Id}/comments");

        var visibleToRequester = Assert.Single(await requesterView.ReadJsonAsync<List<CommentResponse>>());
        Assert.Equal("I'm looking into this now.", visibleToRequester.Body);
        Assert.True(visibleToRequester.AuthorIsStaff);

        var visibleToStaff = await staffView.ReadJsonAsync<List<CommentResponse>>();
        Assert.Equal(2, visibleToStaff.Count);
        Assert.Contains(visibleToStaff, c => c.IsInternal);
    }

    [Fact]
    public async Task Comment_RequesterCannotPostInternalNotes()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();

        using var response = await thandi.CommentAsync(ticket.Id, "Secret note", isInternal: true);

        Assert.Equal("internal_comments_staff_only", (await response.AssertProblemAsync(HttpStatusCode.Forbidden)).Code());
    }

    [Fact]
    public async Task Comment_OnAClosedTicket_Returns409()
    {
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await sipho.CreateTicketAsync();
        await sipho.ChangeStatusAsync(ticket, TicketStatus.Closed, "Raised by mistake.").ExpectTicketAsync();

        using var response = await sipho.CommentAsync(ticket.Id, "One more thing...");

        Assert.Equal("ticket_closed", (await response.AssertProblemAsync(HttpStatusCode.Conflict)).Code());
    }

    [Fact]
    public async Task Comment_WithEmptyBody_Returns400()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var ticket = await thandi.CreateTicketAsync();

        using var response = await thandi.CommentAsync(ticket.Id, "   ");

        Assert.Contains("body", (await response.AssertProblemAsync(HttpStatusCode.BadRequest)).Errors.Keys);
    }

    [Fact]
    public async Task Comment_MovesTheTicketToTheTopOfRecentlyUpdated()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        var older = await thandi.CreateTicketAsync();
        await thandi.CreateTicketAsync();

        (await thandi.CommentAsync(older.Id, "Any update on this?")).Dispose();

        Assert.Equal(older.Id, (await thandi.ListTicketsAsync("?sortBy=updatedAt")).Items[0].Id);
    }

    // ---------- Agent deactivation ----------

    [Fact]
    public async Task DeactivatingAnAgent_ReturnsTheirUnfinishedTicketsToTheQueue()
    {
        var agent = await factory.CreateUserAsync(AppRoles.SupportAgent);
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var admin = await factory.SignedInClientAsync(Lerato);
        var ticket = await thandi.CreateTicketAsync();
        ticket = await admin.AssignAsync(ticket, agent.Id).ExpectTicketAsync();
        await admin.ChangeStatusAsync(ticket, TicketStatus.InProgress).ExpectTicketAsync();

        using var deactivate = await admin.PutJsonAsync($"/api/users/{agent.Id}",
            new UpdateUserRequest { DisplayName = "Test SupportAgent", Role = AppRoles.SupportAgent, IsActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var after = await admin.GetTicketAsync(ticket.Id);
        Assert.Null(after.AssignedAgent);
        Assert.Equal(TicketStatus.Open, after.Status);

        using var historyResponse = await admin.GetPathAsync($"/api/tickets/{ticket.Id}/history");
        Assert.Contains("deactivated", (await historyResponse.ReadJsonAsync<List<StatusHistoryResponse>>())[^1].Note, StringComparison.Ordinal);
    }

    // ---------- Permissions the UI receives ----------

    [Fact]
    public async Task Permissions_DifferForRequesterAndStaff()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();

        var requesterView = (await thandi.GetTicketAsync(ticket.Id)).Permissions;
        var staffView = (await sipho.GetTicketAsync(ticket.Id)).Permissions;

        Assert.True(requesterView.CanEdit);
        Assert.False(requesterView.CanEditPriority);
        Assert.False(requesterView.CanAssign);
        Assert.False(requesterView.CanAddInternalComment);
        Assert.Empty(requesterView.AllowedStatuses);

        Assert.True(staffView.CanEditPriority);
        Assert.True(staffView.CanAssign);
        Assert.True(staffView.CanAddInternalComment);
        Assert.Equal([TicketStatus.Resolved, TicketStatus.Closed], staffView.AllowedStatuses); // unassigned: no In Progress yet
    }

    private static UpdateTicketRequest EditOf(TicketDetails ticket, string? title = null, TicketPriority? priority = null) => new()
    {
        Title = title ?? ticket.Title,
        Description = ticket.Description,
        CategoryId = ticket.Category.Id,
        Priority = priority ?? ticket.Priority,
        Version = ticket.Version,
    };

    private static async Task<List<AgentSummary>> AgentsAsync(HttpClient client)
    {
        using var response = await client.GetPathAsync("/api/agents");
        return await response.ReadJsonAsync<List<AgentSummary>>();
    }
}
