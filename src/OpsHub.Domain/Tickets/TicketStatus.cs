namespace OpsHub.Domain.Tickets;

/// <summary>
/// Explicit numbers are stored in the database, so never renumber existing values.
/// </summary>
public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    WaitingForInformation = 3,
    Resolved = 4,
    Closed = 5,
}
