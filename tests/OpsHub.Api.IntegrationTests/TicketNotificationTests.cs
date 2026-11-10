using OpsHub.Api.Features.Tickets;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>
/// Who hears about a ticket change by email. Each test uses its own requester and agent, so the
/// emails it looks for can only come from that test.
/// </summary>
public class TicketNotificationTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    [Fact]
    public async Task AnAgentsReply_EmailsTheRequester_ButAnInternalNoteDoesNot()
    {
        var (requester, agent, ticket) = await AssignedTicketAsync();
        using var agentClient = await factory.SignedInClientAsync(agent.Email);

        await agentClient.CommentAsync(ticket.Id, "Internal: checking the gateway logs.", isInternal: true);
        await agentClient.CommentAsync(ticket.Id, "Please restart your laptop and try again.");

        var email = await factory.Mail.WaitForAsync(m => m.To == requester.Email && m.Kind == "reply");
        Assert.Single(factory.Mail.To(requester.Email));                                   // not the internal note
        Assert.Contains("Please restart your laptop", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("gateway logs", email.Body, StringComparison.Ordinal);
        Assert.Contains($"{EmailApiFactory.BaseUrl}/tickets/{ticket.Id}", email.Body, StringComparison.Ordinal);
        Assert.StartsWith($"[{ticket.Reference}] New reply: ", email.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRequestersReply_EmailsTheAssignedAgent()
    {
        var (requester, agent, ticket) = await AssignedTicketAsync();
        using var requesterClient = await factory.SignedInClientAsync(requester.Email);

        await requesterClient.CommentAsync(ticket.Id, "It happens on my home Wi-Fi too.");

        var email = await factory.Mail.WaitForAsync(m => m.To == agent.Email && m.Kind == "requester-replied");
        Assert.Contains("home Wi-Fi", email.Body, StringComparison.Ordinal);
        Assert.Empty(factory.Mail.To(requester.Email)); // not about their own reply
    }

    [Fact]
    public async Task Resolving_EmailsTheRequester_WithTheNote()
    {
        var (requester, agent, ticket) = await AssignedTicketAsync();
        using var agentClient = await factory.SignedInClientAsync(agent.Email);
        ticket = await agentClient.ChangeStatusAsync(ticket, TicketStatus.InProgress).ExpectTicketAsync();

        await agentClient.ChangeStatusAsync(ticket, TicketStatus.Resolved, "Replaced the faulty access point.").ExpectTicketAsync();

        var email = await factory.Mail.WaitForAsync(m => m.To == requester.Email && m.Kind == "status-Resolved");
        Assert.Contains("Please confirm the fix", email.Body, StringComparison.Ordinal);
        Assert.Contains("Replaced the faulty access point.", email.Body, StringComparison.Ordinal);
        Assert.Single(factory.Mail.To(requester.Email)); // "In progress" isn't worth an email
    }

    [Fact]
    public async Task Reopening_EmailsTheAgent()
    {
        var (requester, agent, ticket) = await AssignedTicketAsync();
        using var agentClient = await factory.SignedInClientAsync(agent.Email);
        using var requesterClient = await factory.SignedInClientAsync(requester.Email);
        ticket = await agentClient.ChangeStatusAsync(ticket, TicketStatus.InProgress).ExpectTicketAsync();
        ticket = await agentClient.ChangeStatusAsync(ticket, TicketStatus.Resolved).ExpectTicketAsync();
        ticket = await requesterClient.GetTicketAsync(ticket.Id);

        await requesterClient.ChangeStatusAsync(ticket, TicketStatus.Open, "Still dropping every few minutes.").ExpectTicketAsync();

        var email = await factory.Mail.WaitForAsync(m => m.To == agent.Email && m.Kind == "reopened");
        Assert.Contains("Still dropping", email.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeingAssignedBySomeoneElse_EmailsTheAgent_ButPickingATicketUpYourselfDoesNot()
    {
        var requester = await factory.CreateUserAsync(AppRoles.Requester);
        var assignedByAdmin = await factory.CreateUserAsync(AppRoles.SupportAgent);
        var selfAssigned = await factory.CreateUserAsync(AppRoles.SupportAgent);
        using var requesterClient = await factory.SignedInClientAsync(requester.Email);
        using var admin = await factory.SignedInClientAsync("lerato@marula.example");
        using var agentClient = await factory.SignedInClientAsync(selfAssigned.Email);
        var first = await requesterClient.CreateTicketAsync();
        var second = await requesterClient.CreateTicketAsync();

        await agentClient.AssignAsync(second, selfAssigned.Id).ExpectTicketAsync();
        await admin.AssignAsync(first, assignedByAdmin.Id).ExpectTicketAsync();

        var email = await factory.Mail.WaitForAsync(m => m.To == assignedByAdmin.Email && m.Kind == "assigned");
        Assert.Contains("Lerato Dlamini assigned", email.Body, StringComparison.Ordinal);
        Assert.Empty(factory.Mail.To(selfAssigned.Email)); // sent earlier, so it would have arrived first
    }

    [Fact]
    public async Task ADeactivatedRequester_IsNotEmailed()
    {
        var (requester, agent, ticket) = await AssignedTicketAsync();
        var marker = await AssignedTicketAsync(agent);
        using var admin = await factory.SignedInClientAsync("lerato@marula.example");
        using var agentClient = await factory.SignedInClientAsync(agent.Email);
        using var deactivate = await admin.PutJsonAsync($"/api/users/{requester.Id}",
            new { displayName = "Test Requester", role = AppRoles.Requester, isActive = false });
        deactivate.EnsureSuccessStatusCode();

        await agentClient.CommentAsync(ticket.Id, "Following up.");
        await agentClient.CommentAsync(marker.Ticket.Id, "Following up.");

        await factory.Mail.WaitForAsync(m => m.To == marker.Requester.Email);
        Assert.Empty(factory.Mail.To(requester.Email));
    }

    /// <summary>A new requester's ticket, assigned to a new (or the given) agent.</summary>
    private async Task<(TestUser Requester, TestUser Agent, TicketDetails Ticket)> AssignedTicketAsync(TestUser? agent = null)
    {
        var requester = await factory.CreateUserAsync(AppRoles.Requester);
        agent ??= await factory.CreateUserAsync(AppRoles.SupportAgent);
        using var requesterClient = await factory.SignedInClientAsync(requester.Email);
        using var admin = await factory.SignedInClientAsync("lerato@marula.example");
        var ticket = await requesterClient.CreateTicketAsync();
        ticket = await admin.AssignAsync(ticket, agent.Id).ExpectTicketAsync();
        await factory.Mail.WaitForAsync(m => m.To == agent.Email && m.Kind == "assigned"); // so later checks start clean
        return (requester, agent, ticket);
    }
}
