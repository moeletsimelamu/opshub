using OpsHub.Api.Features.Tickets;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Dashboard;

/// <summary>Everything the staff dashboard shows, computed from live ticket data in one request.</summary>
public sealed record DashboardResponse(
    DateTime GeneratedAt,
    DashboardTotals Totals,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<PriorityCount> OpenByPriority,
    IReadOnlyList<AgentWorkload> AgentWorkload,
    IReadOnlyList<TicketListItem> RecentlyUpdated,
    ResolutionTime ResolutionTime);

/// <param name="Open">Active tickets: Open, In Progress or Waiting for Information.</param>
/// <param name="NeedsTriage">Open and nobody assigned.</param>
/// <param name="WaitingForRequester">Waiting for Information.</param>
/// <param name="CriticalOpen">Active tickets with Critical priority.</param>
/// <param name="AwaitingConfirmation">Resolved, waiting for the requester to confirm or reopen.</param>
/// <param name="Total">Every ticket ever logged.</param>
public sealed record DashboardTotals(
    int Open,
    int NeedsTriage,
    int WaitingForRequester,
    int CriticalOpen,
    int AwaitingConfirmation,
    int Total);

public sealed record StatusCount(TicketStatus Status, int Count);

public sealed record PriorityCount(TicketPriority Priority, int Count);

/// <param name="ActiveTickets">Assigned tickets that are Open, In Progress or Waiting.</param>
/// <param name="UrgentTickets">Of those, High or Critical priority.</param>
/// <param name="ResolvedInWindow">Tickets this agent resolved within the resolution window.</param>
public sealed record AgentWorkload(Guid Id, string DisplayName, int ActiveTickets, int UrgentTickets, int ResolvedInWindow);

/// <summary>
/// Average and median time from creation to resolution over the last WindowDays days.
/// AverageHours and MedianHours are null when fewer than MinimumSampleSize tickets were
/// resolved in the window; the UI then explains why instead of showing a misleading number.
/// </summary>
public sealed record ResolutionTime(
    int WindowDays,
    int SampleSize,
    int MinimumSampleSize,
    double? AverageHours,
    double? MedianHours);
