using OpsHub.Domain.Tickets;
using static OpsHub.Domain.Tickets.TicketStatus;

namespace OpsHub.Domain.Tests;

/// <summary>
/// Unit tests for the status state machine. No database, no HTTP: each test builds a
/// Ticket object in memory, so the whole class runs in milliseconds.
/// </summary>
public class TicketWorkflowTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();

    // ---------- Which transitions exist ----------

    [Theory]
    [InlineData(Open, InProgress)]
    [InlineData(Open, WaitingForInformation)]
    [InlineData(Open, Resolved)]
    [InlineData(InProgress, WaitingForInformation)]
    [InlineData(InProgress, Resolved)]
    [InlineData(WaitingForInformation, InProgress)]
    [InlineData(WaitingForInformation, Resolved)]
    [InlineData(Resolved, Open)]
    [InlineData(Resolved, InProgress)]
    [InlineData(Resolved, Closed)]
    public void Staff_CanMakeEveryDefinedTransition(TicketStatus from, TicketStatus to)
    {
        Assert.Null(TicketWorkflow.Check(from, to, TicketActor.Staff, hasAssignee: true, note: null));
    }

    [Theory]
    [InlineData(Closed, Open)]
    [InlineData(Closed, InProgress)]
    [InlineData(Closed, Resolved)]
    [InlineData(InProgress, Open)]
    [InlineData(InProgress, Closed)]
    [InlineData(WaitingForInformation, Open)]
    [InlineData(WaitingForInformation, Closed)]
    public void UndefinedTransitions_AreRejectedForEveryone(TicketStatus from, TicketStatus to)
    {
        var violation = TicketWorkflow.Check(from, to, TicketActor.Staff, hasAssignee: true, note: "note");

        Assert.NotNull(violation);
        Assert.Equal("transition_not_allowed", violation.Code);
        Assert.Equal(WorkflowViolationKind.NotAllowed, violation.Kind);
    }

    [Fact]
    public void MovingToTheSameStatus_IsRejected()
    {
        var violation = TicketWorkflow.Check(Open, Open, TicketActor.Staff, hasAssignee: true, note: null);

        Assert.Equal("status_unchanged", violation?.Code);
    }

    // ---------- Requester permissions ----------

    [Theory]
    [InlineData(Resolved, Open)]    // reopen: the fix didn't work
    [InlineData(Resolved, Closed)]  // confirm the fix
    public void Requester_CanReopenOrCloseAResolvedTicket(TicketStatus from, TicketStatus to)
    {
        Assert.Null(TicketWorkflow.Check(from, to, TicketActor.Requester, hasAssignee: true, note: null));
    }

    [Theory]
    [InlineData(Open, InProgress)]
    [InlineData(Open, Resolved)]
    [InlineData(Open, Closed)]
    [InlineData(InProgress, Resolved)]
    [InlineData(WaitingForInformation, InProgress)]
    [InlineData(Resolved, InProgress)]
    public void Requester_CannotMakeStaffTransitions(TicketStatus from, TicketStatus to)
    {
        var violation = TicketWorkflow.Check(from, to, TicketActor.Requester, hasAssignee: true, note: "note");

        Assert.Equal("transition_requires_staff", violation?.Code);
        Assert.Equal(WorkflowViolationKind.Forbidden, violation?.Kind);
    }

    // ---------- Extra conditions ----------

    [Theory]
    [InlineData(Open, InProgress)]
    [InlineData(Open, WaitingForInformation)]
    [InlineData(Resolved, InProgress)]
    public void WorkingStatuses_RequireAnAssignedAgent(TicketStatus from, TicketStatus to)
    {
        var violation = TicketWorkflow.Check(from, to, TicketActor.Staff, hasAssignee: false, note: null);

        Assert.Equal("assignee_required", violation?.Code);
    }

    [Fact]
    public void ResolvingWithoutAnAssignee_IsAllowed()
    {
        // e.g. an agent answers a quick question straight from the queue.
        Assert.Null(TicketWorkflow.Check(Open, Resolved, TicketActor.Staff, hasAssignee: false, note: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClosingAnOpenTicketWithoutResolving_RequiresANote(string? note)
    {
        var violation = TicketWorkflow.Check(Open, Closed, TicketActor.Staff, hasAssignee: false, note);

        Assert.Equal("note_required", violation?.Code);
        Assert.Equal(WorkflowViolationKind.InputRequired, violation?.Kind);
    }

    [Fact]
    public void ClosingAnOpenTicket_WithANote_IsAllowed()
    {
        Assert.Null(TicketWorkflow.Check(Open, Closed, TicketActor.Staff, hasAssignee: false, "Duplicate of OPS-000012."));
    }

    // ---------- AllowedTargets (what the UI offers) ----------

    [Fact]
    public void AllowedTargets_ForStaffOnAnUnassignedOpenTicket_ExcludeWorkingStatuses()
    {
        var targets = TicketWorkflow.AllowedTargets(Open, TicketActor.Staff, hasAssignee: false);

        Assert.Equal([Resolved, Closed], targets);
    }

    [Fact]
    public void AllowedTargets_ForRequester_AreEmptyUntilResolved()
    {
        Assert.Empty(TicketWorkflow.AllowedTargets(InProgress, TicketActor.Requester, hasAssignee: true));
        Assert.Equal([Open, Closed], TicketWorkflow.AllowedTargets(Resolved, TicketActor.Requester, hasAssignee: true));
    }

    [Fact]
    public void AllowedTargets_ForClosedTicket_AreEmpty()
    {
        Assert.Empty(TicketWorkflow.AllowedTargets(Closed, TicketActor.Staff, hasAssignee: true));
    }

    // ---------- Applying a change ----------

    [Fact]
    public void ChangeStatus_RecordsHistoryAndUpdatesTimestamp()
    {
        var ticket = NewTicket(Open, assigned: true);

        var violation = TicketWorkflow.ChangeStatus(ticket, InProgress, TicketActor.Staff, AgentId, "  Looking into it  ", Now);

        Assert.Null(violation);
        Assert.Equal(InProgress, ticket.Status);
        Assert.Equal(Now, ticket.UpdatedAt);
        var entry = Assert.Single(ticket.StatusHistory);
        Assert.Equal(Open, entry.FromStatus);
        Assert.Equal(InProgress, entry.ToStatus);
        Assert.Equal(AgentId, entry.ChangedById);
        Assert.Equal("Looking into it", entry.Note);
        Assert.Equal(Now, entry.ChangedAt);
    }

    [Fact]
    public void ChangeStatus_WhenRefused_ChangesNothing()
    {
        var ticket = NewTicket(Closed, assigned: true);
        var updatedBefore = ticket.UpdatedAt;

        var violation = TicketWorkflow.ChangeStatus(ticket, Open, TicketActor.Staff, AgentId, null, Now);

        Assert.NotNull(violation);
        Assert.Equal(Closed, ticket.Status);
        Assert.Equal(updatedBefore, ticket.UpdatedAt);
        Assert.Empty(ticket.StatusHistory);
    }

    [Fact]
    public void Resolving_SetsResolvedAt_AndReopening_ClearsIt()
    {
        var ticket = NewTicket(InProgress, assigned: true);

        TicketWorkflow.ChangeStatus(ticket, Resolved, TicketActor.Staff, AgentId, null, Now);
        Assert.Equal(Now, ticket.ResolvedAt);

        TicketWorkflow.ChangeStatus(ticket, Open, TicketActor.Requester, RequesterId, "Still broken", Now.AddHours(1));
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void ClosingAResolvedTicket_KeepsTheResolutionTime()
    {
        var ticket = NewTicket(InProgress, assigned: true);
        TicketWorkflow.ChangeStatus(ticket, Resolved, TicketActor.Staff, AgentId, null, Now);

        TicketWorkflow.ChangeStatus(ticket, Closed, TicketActor.Requester, RequesterId, null, Now.AddDays(1));

        Assert.Equal(Now, ticket.ResolvedAt);
    }

    [Fact]
    public void ClosingAnOpenTicket_LeavesResolvedAtEmpty()
    {
        var ticket = NewTicket(Open, assigned: false);

        TicketWorkflow.ChangeStatus(ticket, Closed, TicketActor.Staff, AgentId, "Not an IT request", Now);

        Assert.Null(ticket.ResolvedAt);
    }

    // ---------- Automatic rules ----------

    [Fact]
    public void RequesterReply_OnWaitingTicket_MovesItBackToInProgress()
    {
        var ticket = NewTicket(WaitingForInformation, assigned: true);

        var resumed = TicketWorkflow.ResumeAfterRequesterReply(ticket, RequesterId, Now);

        Assert.True(resumed);
        Assert.Equal(InProgress, ticket.Status);
        Assert.Equal(RequesterId, Assert.Single(ticket.StatusHistory).ChangedById);
    }

    [Theory]
    [InlineData(Open)]
    [InlineData(InProgress)]
    [InlineData(Resolved)]
    public void RequesterReply_OnOtherStatuses_ChangesNothing(TicketStatus status)
    {
        var ticket = NewTicket(status, assigned: true);

        Assert.False(TicketWorkflow.ResumeAfterRequesterReply(ticket, RequesterId, Now));
        Assert.Equal(status, ticket.Status);
        Assert.Empty(ticket.StatusHistory);
    }

    [Theory]
    [InlineData(InProgress)]
    [InlineData(WaitingForInformation)]
    public void ReturnToQueue_OnAWorkingTicket_UnassignsAndReopens(TicketStatus status)
    {
        var ticket = NewTicket(status, assigned: true);

        TicketWorkflow.ReturnToQueue(ticket, AgentId, "Agent deactivated.", Now);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Open, ticket.Status);
        Assert.Equal("Agent deactivated.", Assert.Single(ticket.StatusHistory).Note);
    }

    [Fact]
    public void ReturnToQueue_OnAnOpenTicket_OnlyUnassigns()
    {
        var ticket = NewTicket(Open, assigned: true);

        TicketWorkflow.ReturnToQueue(ticket, AgentId, "Unassigned.", Now);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Open, ticket.Status);
        Assert.Empty(ticket.StatusHistory);
    }

    private static Ticket NewTicket(TicketStatus status, bool assigned) => new()
    {
        Id = 42,
        Title = "Printer offline",
        Description = "The printer on floor 2 is offline.",
        Status = status,
        RequesterId = RequesterId,
        AssignedAgentId = assigned ? AgentId : null,
        CreatedAt = Now.AddDays(-1),
        UpdatedAt = Now.AddDays(-1),
        ResolvedAt = status == Resolved ? Now.AddHours(-2) : null,
    };
}
