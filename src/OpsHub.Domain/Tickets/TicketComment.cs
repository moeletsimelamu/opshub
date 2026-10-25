using OpsHub.Domain.Users;

namespace OpsHub.Domain.Tickets;

public class TicketComment
{
    public const int BodyMaxLength = 4000;

    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    /// <summary>Internal notes are visible to support staff only, never to the requester.</summary>
    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; }
}
