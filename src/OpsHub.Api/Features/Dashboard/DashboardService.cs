using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Data;
using OpsHub.Api.Features.Tickets;
using OpsHub.Domain.Reporting;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Dashboard;

/// <summary>
/// Dashboard metrics. Every number is computed from the database at request time; nothing
/// is cached or hard-coded. Counting happens in SQL (GROUP BY / COUNT); only the
/// resolution-time maths runs in C#, because SQLite cannot subtract dates through EF Core.
/// </summary>
public sealed class DashboardService(OpsHubDbContext db, TimeProvider timeProvider)
{
    private const int RecentlyUpdatedCount = 6;

    private static readonly TicketStatus[] ActiveStatuses =
        [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingForInformation];

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = now.AddDays(-ResolutionStatistics.DefaultWindowDays);
        var tickets = db.Tickets.AsNoTracking();

        // ---------- Counts by status (every status listed, including zeros) ----------
        var statusCounts = await tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var byStatus = Enum.GetValues<TicketStatus>()
            .Select(s => new StatusCount(s, statusCounts.GetValueOrDefault(s)))
            .ToList();

        // ---------- Active tickets by priority ----------
        var priorityCounts = await tickets
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Priority, x => x.Count, cancellationToken);
        var openByPriority = Enum.GetValues<TicketPriority>()
            .Select(p => new PriorityCount(p, priorityCounts.GetValueOrDefault(p)))
            .ToList();

        // ---------- Headline totals ----------
        var totals = new DashboardTotals(
            Open: ActiveStatuses.Sum(s => statusCounts.GetValueOrDefault(s)),
            NeedsTriage: await tickets.CountAsync(t => t.Status == TicketStatus.Open && t.AssignedAgentId == null, cancellationToken),
            WaitingForRequester: statusCounts.GetValueOrDefault(TicketStatus.WaitingForInformation),
            CriticalOpen: priorityCounts.GetValueOrDefault(TicketPriority.Critical),
            AwaitingConfirmation: statusCounts.GetValueOrDefault(TicketStatus.Resolved),
            Total: statusCounts.Values.Sum());

        // ---------- Workload per active support agent ----------
        var agentWorkload = await (
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == AppRoles.SupportAgent && user.IsActive
            select new AgentWorkload(
                user.Id,
                user.DisplayName,
                tickets.Count(t => t.AssignedAgentId == user.Id && ActiveStatuses.Contains(t.Status)),
                tickets.Count(t => t.AssignedAgentId == user.Id && ActiveStatuses.Contains(t.Status)
                                   && (t.Priority == TicketPriority.High || t.Priority == TicketPriority.Critical)),
                tickets.Count(t => t.AssignedAgentId == user.Id && t.ResolvedAt != null && t.ResolvedAt > windowStart)))
            .ToListAsync(cancellationToken);
        agentWorkload = agentWorkload
            .OrderByDescending(a => a.ActiveTickets)
            .ThenBy(a => a.DisplayName, StringComparer.CurrentCulture)
            .ToList();

        // ---------- Recently updated ----------
        var recentlyUpdated = await tickets
            .OrderByDescending(t => t.UpdatedAt)
            .ThenByDescending(t => t.Id)
            .Take(RecentlyUpdatedCount)
            .SelectListItem()
            .ToListAsync(cancellationToken);

        // ---------- Resolution time (only the two columns needed are read) ----------
        var resolved = await tickets
            .Where(t => t.ResolvedAt != null && t.ResolvedAt > windowStart)
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value })
            .ToListAsync(cancellationToken);
        var summary = ResolutionStatistics.Calculate(
            resolved.Select(r => new ResolvedTicketTimes(r.CreatedAt, r.ResolvedAt)),
            now);

        return new DashboardResponse(
            now,
            totals,
            byStatus,
            openByPriority,
            agentWorkload,
            recentlyUpdated,
            new ResolutionTime(
                summary.WindowDays,
                summary.SampleSize,
                summary.MinimumSampleSize,
                summary.Average?.TotalHours,
                summary.Median?.TotalHours));
    }
}
