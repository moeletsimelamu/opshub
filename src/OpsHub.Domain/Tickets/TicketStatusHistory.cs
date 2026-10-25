using OpsHub.Domain.Users;

namespace OpsHub.Domain.Tickets;

/// <summary>
/// One row per status change: an append-only audit trail of who moved the
/// ticket, from which status to which, and when.
/// </summary>
public class TicketStatusHistory
{
    public const int NoteMaxLength = 500;

    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    /// <summary>Null for the first row, written when the ticket is created.</summary>
    public TicketStatus? FromStatus { get; set; }

    public TicketStatus ToStatus { get; set; }

    public Guid ChangedById { get; set; }
    public AppUser ChangedBy { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }
}
