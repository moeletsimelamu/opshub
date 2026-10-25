namespace OpsHub.Domain.Tickets;

/// <summary>Who is asking for a change. Staff = Support Agent or Administrator.</summary>
public enum TicketActor
{
    Requester,
    Staff,
}

public enum WorkflowViolationKind
{
    /// <summary>The change is impossible from the ticket's current state (HTTP 409).</summary>
    NotAllowed,

    /// <summary>The change is possible, but not for this kind of user (HTTP 403).</summary>
    Forbidden,

    /// <summary>The change needs more input, e.g. a note (HTTP 400).</summary>
    InputRequired,
}

/// <summary>Why a status change was refused. Code is stable for clients; Message is for people.</summary>
public sealed record WorkflowViolation(WorkflowViolationKind Kind, string Code, string Message);

/// <summary>
/// The ticket status state machine. Every status change in OpsHub goes through this class,
/// so the rules live in one place and are covered by fast unit tests.
///
///   From                     → To
///   Open                     → In Progress*, Waiting for Information*, Resolved, Closed (needs a note)
///   In Progress              → Waiting for Information, Resolved
///   Waiting for Information  → In Progress, Resolved
///   Resolved                 → Open (reopen), In Progress*, Closed
///   Closed                   → (final)
///
///   * requires an assigned agent. Requesters may only reopen or close their own Resolved ticket.
/// </summary>
public static class TicketWorkflow
{
    /// <summary>Every transition that exists at all (what staff may do).</summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Transitions = new()
    {
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.WaitingForInformation, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.WaitingForInformation, TicketStatus.Resolved],
        [TicketStatus.WaitingForInformation] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.Closed] = [],
    };

    /// <summary>
    /// The subset a requester may perform on their own ticket: once it is resolved, they
    /// either reopen it (the fix didn't work) or close it (they confirm the fix).
    /// </summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> RequesterTransitions = new()
    {
        [TicketStatus.Resolved] = [TicketStatus.Open, TicketStatus.Closed],
    };

    /// <summary>Someone has to be working on a ticket that is In Progress or Waiting for Information.</summary>
    public static bool RequiresAssignee(TicketStatus status) =>
        status is TicketStatus.InProgress or TicketStatus.WaitingForInformation;

    /// <summary>Active tickets count towards an agent's workload and can be worked on.</summary>
    public static bool IsActive(TicketStatus status) =>
        status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingForInformation;

    /// <summary>Returns why the change is not allowed, or null if it is.</summary>
    public static WorkflowViolation? Check(TicketStatus from, TicketStatus to, TicketActor actor, bool hasAssignee, string? note)
    {
        if (from == to)
        {
            return new(WorkflowViolationKind.NotAllowed, "status_unchanged", $"The ticket is already {Describe(to)}.");
        }

        if (!Transitions[from].Contains(to))
        {
            return new(WorkflowViolationKind.NotAllowed, "transition_not_allowed",
                $"A ticket cannot move from {Describe(from)} to {Describe(to)}.");
        }

        if (actor == TicketActor.Requester &&
            !(RequesterTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to)))
        {
            return new(WorkflowViolationKind.Forbidden, "transition_requires_staff",
                "Only support staff can make this status change.");
        }

        if (RequiresAssignee(to) && !hasAssignee)
        {
            return new(WorkflowViolationKind.NotAllowed, "assignee_required",
                $"Assign an agent before moving the ticket to {Describe(to)}.");
        }

        if (from == TicketStatus.Open && to == TicketStatus.Closed && actor == TicketActor.Staff && string.IsNullOrWhiteSpace(note))
        {
            return new(WorkflowViolationKind.InputRequired, "note_required",
                "Add a note explaining why the ticket is being closed without being resolved.");
        }

        return null;
    }

    /// <summary>
    /// The statuses this actor could move the ticket to right now. The UI uses this to
    /// show only the buttons that will work; the API still calls <see cref="ChangeStatus"/>.
    /// A transition that only needs a note is included (the UI asks for the note).
    /// </summary>
    public static IReadOnlyList<TicketStatus> AllowedTargets(TicketStatus from, TicketActor actor, bool hasAssignee) =>
        Transitions[from]
            .Where(to => Check(from, to, actor, hasAssignee, note: "(provided by the user)") is null)
            .ToList();

    /// <summary>
    /// Validates and applies a status change requested by a user: updates the status,
    /// keeps ResolvedAt consistent and appends a history row. Returns the violation
    /// instead of changing anything if the rules forbid it.
    /// </summary>
    public static WorkflowViolation? ChangeStatus(Ticket ticket, TicketStatus to, TicketActor actor, Guid changedById, string? note, DateTime now)
    {
        var violation = Check(ticket.Status, to, actor, ticket.AssignedAgentId is not null, note);
        if (violation is null)
        {
            Record(ticket, to, changedById, note, now);
        }

        return violation;
    }

    /// <summary>
    /// Automatic rule: when the requester replies to a ticket that is waiting for their
    /// information, the ticket goes back to In Progress so the agent sees it again.
    /// </summary>
    public static bool ResumeAfterRequesterReply(Ticket ticket, Guid requesterId, DateTime now)
    {
        if (ticket.Status != TicketStatus.WaitingForInformation)
        {
            return false;
        }

        Record(ticket, TicketStatus.InProgress, requesterId, "Requester replied.", now);
        return true;
    }

    /// <summary>
    /// Removes the assigned agent. A ticket that was being worked on (In Progress or
    /// Waiting) cannot stay in that status without an agent, so it goes back to Open,
    /// the triage queue.
    /// </summary>
    public static void ReturnToQueue(Ticket ticket, Guid changedById, string reason, DateTime now)
    {
        ticket.AssignedAgentId = null;
        ticket.UpdatedAt = now;

        if (RequiresAssignee(ticket.Status))
        {
            Record(ticket, TicketStatus.Open, changedById, reason, now);
        }
    }

    public static string Describe(TicketStatus status) => status switch
    {
        TicketStatus.Open => "Open",
        TicketStatus.InProgress => "In Progress",
        TicketStatus.WaitingForInformation => "Waiting for Information",
        TicketStatus.Resolved => "Resolved",
        TicketStatus.Closed => "Closed",
        _ => status.ToString(),
    };

    private static void Record(Ticket ticket, TicketStatus to, Guid changedById, string? note, DateTime now)
    {
        var from = ticket.Status;
        ticket.Status = to;
        ticket.UpdatedAt = now;

        // Entering Resolved stamps the resolution time; leaving it for an active status
        // (reopen) clears it; closing keeps whatever it was (null if never resolved).
        ticket.ResolvedAt = to switch
        {
            TicketStatus.Resolved => now,
            TicketStatus.Closed => ticket.ResolvedAt,
            _ => null,
        };

        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            TicketId = ticket.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedById = changedById,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ChangedAt = now,
        });
    }
}
