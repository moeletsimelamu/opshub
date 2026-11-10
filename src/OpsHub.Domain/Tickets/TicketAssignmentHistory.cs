using OpsHub.Domain.Users;

namespace OpsHub.Domain.Tickets;

/// <summary>
/// One row per change of assigned agent: an append-only record of who handed the ticket to
/// whom, and when. Together with <see cref="TicketStatusHistory"/> it tells the ticket's story.
/// </summary>
public class TicketAssignmentHistory
{
    public const int NoteMaxLength = TicketStatusHistory.NoteMaxLength;

    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    /// <summary>Null when the ticket had no agent before (first assignment).</summary>
    public Guid? FromAgentId { get; set; }
    public AppUser? FromAgent { get; set; }

    /// <summary>Null when the ticket was unassigned and returned to the queue.</summary>
    public Guid? ToAgentId { get; set; }
    public AppUser? ToAgent { get; set; }

    public Guid ChangedById { get; set; }
    public AppUser ChangedBy { get; set; } = null!;

    /// <summary>Why it happened, when the system did it (e.g. the agent was deactivated).</summary>
    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }
}
