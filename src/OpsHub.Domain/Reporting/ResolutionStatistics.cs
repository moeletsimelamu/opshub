namespace OpsHub.Domain.Reporting;

/// <summary>Created/resolved timestamps of one resolved ticket (UTC).</summary>
public readonly record struct ResolvedTicketTimes(DateTime CreatedAt, DateTime ResolvedAt);

/// <summary>
/// How long tickets took to resolve over a recent window. When there aren't enough
/// resolved tickets the average is withheld (null) rather than shown: an "average"
/// of one or two tickets would look like a trend but mean nothing.
/// </summary>
public sealed record ResolutionSummary(
    int WindowDays,
    int SampleSize,
    int MinimumSampleSize,
    TimeSpan? Average,
    TimeSpan? Median)
{
    public bool HasEnoughData => Average is not null;
}

public static class ResolutionStatistics
{
    public const int DefaultWindowDays = 30;
    public const int DefaultMinimumSampleSize = 3;

    /// <summary>
    /// Averages (ResolvedAt − CreatedAt) for tickets resolved within the last
    /// <paramref name="windowDays"/> days before <paramref name="now"/>. Tickets resolved
    /// "in the future" (clock skew) or before the window are ignored.
    /// </summary>
    public static ResolutionSummary Calculate(
        IEnumerable<ResolvedTicketTimes> tickets,
        DateTime now,
        int windowDays = DefaultWindowDays,
        int minimumSampleSize = DefaultMinimumSampleSize)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentOutOfRangeException.ThrowIfLessThan(windowDays, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumSampleSize, 1);

        var windowStart = now.AddDays(-windowDays);
        var durations = tickets
            .Where(t => t.ResolvedAt > windowStart && t.ResolvedAt <= now && t.ResolvedAt >= t.CreatedAt)
            .Select(t => t.ResolvedAt - t.CreatedAt)
            .OrderBy(d => d)
            .ToList();

        if (durations.Count < minimumSampleSize)
        {
            return new ResolutionSummary(windowDays, durations.Count, minimumSampleSize, null, null);
        }

        var average = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
        var middle = durations.Count / 2;
        var median = durations.Count % 2 == 1
            ? durations[middle]
            : TimeSpan.FromTicks((durations[middle - 1].Ticks + durations[middle].Ticks) / 2);

        return new ResolutionSummary(windowDays, durations.Count, minimumSampleSize, average, median);
    }
}
