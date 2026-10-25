namespace OpsHub.Domain.Tickets;

/// <summary>
/// Stored as integers so that sorting by priority orders Low → Critical
/// (as text, "Critical" would sort before "Low").
/// </summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}
