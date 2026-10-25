using OpsHub.Domain.Reporting;

namespace OpsHub.Domain.Tests;

/// <summary>Exact-value tests for the dashboard's resolution-time calculation, with a fixed "now".</summary>
public class ResolutionStatisticsTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A ticket resolved <paramref name="daysAgo"/> days ago after <paramref name="hours"/> hours of work.</summary>
    private static ResolvedTicketTimes Resolved(double daysAgo, double hours)
    {
        var resolvedAt = Now.AddDays(-daysAgo);
        return new ResolvedTicketTimes(resolvedAt.AddHours(-hours), resolvedAt);
    }

    [Fact]
    public void Average_IsTheMeanOfResolutionDurations()
    {
        var summary = ResolutionStatistics.Calculate([Resolved(1, 2), Resolved(2, 4), Resolved(3, 9)], Now);

        Assert.True(summary.HasEnoughData);
        Assert.Equal(3, summary.SampleSize);
        Assert.Equal(TimeSpan.FromHours(5), summary.Average);   // (2 + 4 + 9) / 3
        Assert.Equal(TimeSpan.FromHours(4), summary.Median);
    }

    [Fact]
    public void Median_WithEvenSample_IsTheMeanOfTheMiddleTwo()
    {
        var summary = ResolutionStatistics.Calculate([Resolved(1, 1), Resolved(1, 3), Resolved(1, 5), Resolved(1, 100)], Now);

        Assert.Equal(TimeSpan.FromHours(4), summary.Median);       // (3 + 5) / 2
        Assert.Equal(TimeSpan.FromHours(27.25), summary.Average);  // the outlier pulls the mean up
    }

    [Fact]
    public void TicketsResolvedBeforeTheWindow_AreIgnored()
    {
        var summary = ResolutionStatistics.Calculate(
            [Resolved(1, 2), Resolved(2, 4), Resolved(3, 6), Resolved(45, 500)],
            Now,
            windowDays: 30);

        Assert.Equal(3, summary.SampleSize);
        Assert.Equal(TimeSpan.FromHours(4), summary.Average);
    }

    [Fact]
    public void WindowBoundary_ExactlyWindowDaysAgo_IsExcluded()
    {
        var summary = ResolutionStatistics.Calculate([Resolved(30, 1), Resolved(29.99, 2), Resolved(1, 3), Resolved(1, 4)], Now, windowDays: 30);

        Assert.Equal(3, summary.SampleSize);
    }

    [Fact]
    public void TooFewResolvedTickets_WithholdsTheAverage()
    {
        var summary = ResolutionStatistics.Calculate([Resolved(1, 2), Resolved(2, 4)], Now);

        Assert.False(summary.HasEnoughData);
        Assert.Null(summary.Average);
        Assert.Null(summary.Median);
        Assert.Equal(2, summary.SampleSize);
        Assert.Equal(ResolutionStatistics.DefaultMinimumSampleSize, summary.MinimumSampleSize);
    }

    [Fact]
    public void NoTickets_WithholdsTheAverage()
    {
        var summary = ResolutionStatistics.Calculate([], Now);

        Assert.False(summary.HasEnoughData);
        Assert.Equal(0, summary.SampleSize);
    }

    [Fact]
    public void FutureOrNegativeDurations_AreIgnored()
    {
        ResolvedTicketTimes[] tickets =
        [
            Resolved(1, 2),
            Resolved(1, 2),
            Resolved(1, 2),
            new(Now.AddHours(1), Now.AddHours(2)),      // resolved in the future (clock skew)
            new(Now.AddHours(-1), Now.AddHours(-5)),    // resolved before it was created (bad data)
        ];

        var summary = ResolutionStatistics.Calculate(tickets, Now);

        Assert.Equal(3, summary.SampleSize);
        Assert.Equal(TimeSpan.FromHours(2), summary.Average);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(30, 0)]
    public void InvalidSettings_AreRejected(int windowDays, int minimumSample)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResolutionStatistics.Calculate([], Now, windowDays, minimumSample));
    }
}
