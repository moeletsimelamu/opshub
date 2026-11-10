using System.Net;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Tickets;
using OpsHub.Api.Features.Users;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>Every change of assigned agent is recorded and returned with the ticket's history.</summary>
public class AssignmentHistoryTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static readonly string Thandi = DemoData.EmailFor("thandi");   // Requester
    private static readonly string Pieter = DemoData.EmailFor("pieter");   // Requester
    private static readonly string Sipho = DemoData.EmailFor("sipho");     // Support Agent
    private static readonly string Lerato = DemoData.EmailFor("lerato");   // Administrator

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AssignReassignAndUnassign_AreRecordedInOrder()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var lerato = await factory.SignedInClientAsync(Lerato);
        var siphoId = await IdOfAsync(Sipho);
        var otherAgent = await factory.CreateUserAsync(AppRoles.SupportAgent);
        var ticket = await thandi.CreateTicketAsync();

        ticket = await lerato.AssignAsync(ticket, siphoId).ExpectTicketAsync();
        ticket = await lerato.AssignAsync(ticket, otherAgent.Id).ExpectTicketAsync();
        await lerato.AssignAsync(ticket, null).ExpectTicketAsync();

        var history = await AssignmentsAsync(lerato, ticket.Id);

        Assert.Equal(3, history.Count);
        Assert.Equal((null, siphoId), (history[0].From?.Id, history[0].To?.Id));
        Assert.Equal((siphoId, otherAgent.Id), (history[1].From?.Id, history[1].To?.Id));
        Assert.Equal(otherAgent.Id, history[2].From?.Id);
        Assert.Null(history[2].To);
        Assert.All(history, h => Assert.Equal("Lerato Dlamini", h.ChangedBy.DisplayName));
        Assert.Equal("Unassigned and returned to the queue.", history[2].Note);  // an Open ticket: no status change carries it
    }

    [Fact]
    public async Task AssigningTheSameAgentAgain_RecordsNothing_AndChangesNothing()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var siphoId = await IdOfAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();
        ticket = await sipho.AssignAsync(ticket, siphoId).ExpectTicketAsync();

        var again = await sipho.AssignAsync(ticket, siphoId).ExpectTicketAsync();

        Assert.Equal(ticket.Version, again.Version);
        Assert.Single(await AssignmentsAsync(sipho, ticket.Id));
    }

    [Fact]
    public async Task Requesters_SeeTheAssignmentsOfTheirOwnTickets_Only()
    {
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var pieter = await factory.SignedInClientAsync(Pieter);
        using var sipho = await factory.SignedInClientAsync(Sipho);
        var ticket = await thandi.CreateTicketAsync();
        await sipho.AssignAsync(ticket, await IdOfAsync(Sipho)).ExpectTicketAsync();

        var own = await AssignmentsAsync(thandi, ticket.Id);
        using var someoneElses = await pieter.GetPathAsync($"/api/tickets/{ticket.Id}/assignments");

        Assert.Equal("Sipho Ndlovu", Assert.Single(own).To?.DisplayName);
        Assert.Equal(HttpStatusCode.NotFound, someoneElses.StatusCode); // as if it didn't exist
    }

    [Fact]
    public async Task DeactivatingAnAgent_RecordsTheUnassignment_OfTheirOpenTickets()
    {
        var agent = await factory.CreateUserAsync(AppRoles.SupportAgent);
        using var thandi = await factory.SignedInClientAsync(Thandi);
        using var admin = await factory.SignedInClientAsync(Lerato);
        var ticket = await thandi.CreateTicketAsync();
        await admin.AssignAsync(ticket, agent.Id).ExpectTicketAsync();

        using var deactivate = await admin.PutJsonAsync($"/api/users/{agent.Id}",
            new UpdateUserRequest { DisplayName = "Test SupportAgent", Role = AppRoles.SupportAgent, IsActive = false });
        deactivate.EnsureSuccessStatusCode();

        var last = (await AssignmentsAsync(admin, ticket.Id))[^1];
        Assert.Equal(agent.Id, last.From?.Id);
        Assert.Null(last.To);
        Assert.Contains("deactivated", last.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoData_RecordsWhenEachAgentPickedUpTheirTicket()
    {
        // Only the demo tickets: other tests in this class add tickets to the same database.
        var demoTitles = DemoData.Tickets.Select(t => t.Title).ToList();

        await factory.WithDbAsync(async db =>
        {
            var tickets = await db.Tickets
                .Where(t => t.AssignedAgentId != null && demoTitles.Contains(t.Title))
                .Select(t => new
                {
                    t.AssignedAgentId,
                    Assignments = t.AssignmentHistory.Select(a => new { a.ToAgentId, a.ChangedAt }).ToList(),
                    FirstWork = t.StatusHistory.Where(s => s.FromStatus != null).Min(s => (DateTime?)s.ChangedAt),
                    t.CreatedAt,
                })
                .ToListAsync(Ct);

            Assert.NotEmpty(tickets);
            Assert.All(tickets, t =>
            {
                var assignment = Assert.Single(t.Assignments);
                Assert.Equal(t.AssignedAgentId, assignment.ToAgentId);
                Assert.True(assignment.ChangedAt > t.CreatedAt);
                Assert.True(t.FirstWork is null || assignment.ChangedAt < t.FirstWork, "picked up before work started");
            });
        });
    }

    private async Task<Guid> IdOfAsync(string email)
    {
        using var client = await factory.SignedInClientAsync(email);
        return (await client.MeAsync()).Id;
    }

    private static async Task<List<AssignmentHistoryResponse>> AssignmentsAsync(HttpClient client, int ticketId)
    {
        using var response = await client.GetPathAsync($"/api/tickets/{ticketId}/assignments");
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<List<AssignmentHistoryResponse>>();
    }
}
