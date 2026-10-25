using OpsHub.Domain.Users;

namespace OpsHub.Domain.Tickets;

public class Ticket
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    public int Id { get; set; }

    /// <summary>Human-friendly reference shown in the UI, e.g. OPS-000042.</summary>
    public string Reference => FormatReference(Id);

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public Guid RequesterId { get; set; }
    public AppUser Requester { get; set; } = null!;

    public Guid? AssignedAgentId { get; set; }
    public AppUser? AssignedAgent { get; set; }

    /// <summary>All timestamps are UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Set when the ticket enters Resolved; cleared if it is reopened.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. It changes on every save, so if two people
    /// edit the same ticket, the second save fails instead of silently overwriting.
    /// </summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public List<TicketComment> Comments { get; set; } = [];

    public List<TicketStatusHistory> StatusHistory { get; set; } = [];

    public static string FormatReference(int id) => $"OPS-{id:D6}";
}
