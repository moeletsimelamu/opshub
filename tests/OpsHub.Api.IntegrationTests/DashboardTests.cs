using System.Net;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Dashboard;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>
/// The dashboard must report exactly what is in the database. Expected values are computed
/// from the seed definitions (DemoData), not typed in, so they stay correct if the seed changes.
/// This class never modifies data, so the seed is the whole truth.
/// </summary>
public class DashboardTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static readonly TicketStatus[] Active = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingForInformation];

    private async Task<DashboardResponse> GetDashboardAsync()
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor("sipho"));
        using var response = await client.GetPathAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<DashboardResponse>();
    }

    [Fact]
    public async Task Dashboard_AsRequester_Returns403()
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor("thandi"));

        using var response = await client.GetPathAsync("/api/dashboard");

        await response.AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_Anonymous_Returns401()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync("/api/dashboard");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Totals_MatchTheSeededTickets()
    {
        var seed = DemoData.Tickets;

        var totals = (await GetDashboardAsync()).Totals;

        Assert.Equal(seed.Length, totals.Total);
        Assert.Equal(seed.Count(t => Active.Contains(t.Status)), totals.Open);
        Assert.Equal(seed.Count(t => t.Status == TicketStatus.Open && t.AgentKey is null), totals.NeedsTriage);
        Assert.Equal(seed.Count(t => t.Status == TicketStatus.WaitingForInformation), totals.WaitingForRequester);
        Assert.Equal(seed.Count(t => Active.Contains(t.Status) && t.Priority == TicketPriority.Critical), totals.CriticalOpen);
        Assert.Equal(seed.Count(t => t.Status == TicketStatus.Resolved), totals.AwaitingConfirmation);
    }

    [Fact]
    public async Task ByStatus_ListsEveryStatus_WithSeededCounts()
    {
        var byStatus = (await GetDashboardAsync()).ByStatus;

        Assert.Equal(Enum.GetValues<TicketStatus>(), byStatus.Select(s => s.Status));
        foreach (var entry in byStatus)
        {
            Assert.Equal(DemoData.Tickets.Count(t => t.Status == entry.Status), entry.Count);
        }
        Assert.Equal(DemoData.Tickets.Length, byStatus.Sum(s => s.Count));
    }

    [Fact]
    public async Task OpenByPriority_CountsOnlyActiveTickets()
    {
        var byPriority = (await GetDashboardAsync()).OpenByPriority;

        Assert.Equal(Enum.GetValues<TicketPriority>(), byPriority.Select(p => p.Priority));
        foreach (var entry in byPriority)
        {
            Assert.Equal(DemoData.Tickets.Count(t => Active.Contains(t.Status) && t.Priority == entry.Priority), entry.Count);
        }
    }

    [Fact]
    public async Task AgentWorkload_ListsActiveAgentsOnly_WithTheirActiveTickets()
    {
        var workload = (await GetDashboardAsync()).AgentWorkload;

        var activeAgents = DemoData.Users.Where(u => u.Role == AppRoles.SupportAgent && u.IsActive).ToList();
        Assert.Equal(activeAgents.Count, workload.Count);
        Assert.DoesNotContain(workload, a => a.DisplayName == "Kagiso Molefe"); // deactivated

        foreach (var agent in activeAgents)
        {
            var row = Assert.Single(workload, a => a.DisplayName == agent.DisplayName);
            var assigned = DemoData.Tickets.Where(t => t.AgentKey == agent.Key && Active.Contains(t.Status)).ToList();
            Assert.Equal(assigned.Count, row.ActiveTickets);
            Assert.Equal(assigned.Count(t => t.Priority is TicketPriority.High or TicketPriority.Critical), row.UrgentTickets);
        }

        // Busiest agent first.
        Assert.Equal(workload.OrderByDescending(a => a.ActiveTickets).Select(a => a.ActiveTickets), workload.Select(a => a.ActiveTickets));
    }

    [Fact]
    public async Task ResolutionTime_IsTheAverageOfTicketsResolvedInTheLast30Days()
    {
        // In the seed, a ticket is resolved (CreatedDaysAgo - HoursToResolve/24) days ago.
        var expected = DemoData.Tickets
            .Where(t => t.HoursToResolve is not null && t.Status is TicketStatus.Resolved or TicketStatus.Closed)
            .Where(t => t.CreatedDaysAgo - (t.HoursToResolve!.Value / 24) < 30)
            .Select(t => t.HoursToResolve!.Value)
            .ToList();

        var resolution = (await GetDashboardAsync()).ResolutionTime;

        Assert.Equal(30, resolution.WindowDays);
        Assert.Equal(expected.Count, resolution.SampleSize);
        Assert.NotNull(resolution.AverageHours);
        Assert.Equal(expected.Average(), resolution.AverageHours!.Value, precision: 2);
        Assert.NotNull(resolution.MedianHours);
    }

    [Fact]
    public async Task RecentlyUpdated_IsNewestFirst()
    {
        var recent = (await GetDashboardAsync()).RecentlyUpdated;

        Assert.Equal(6, recent.Count);
        Assert.Equal(recent.OrderByDescending(t => t.UpdatedAt).Select(t => t.Id), recent.Select(t => t.Id));
    }
}

/// <summary>An empty service desk: the dashboard must show zeros and withhold the average.</summary>
public class EmptyDashboardTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task EmptyDatabase_ReturnsZeros_AndNoAverage()
    {
        await factory.EnsureRolesAsync();
        var agent = await factory.CreateUserAsync(AppRoles.SupportAgent);
        using var client = await factory.SignedInClientAsync(agent.Email);

        using var response = await client.GetPathAsync("/api/dashboard");
        var dashboard = await response.ReadJsonAsync<DashboardResponse>();

        Assert.Equal(0, dashboard.Totals.Total);
        Assert.All(dashboard.ByStatus, s => Assert.Equal(0, s.Count));
        Assert.Empty(dashboard.RecentlyUpdated);
        Assert.Equal(0, dashboard.ResolutionTime.SampleSize);
        Assert.Null(dashboard.ResolutionTime.AverageHours);
        Assert.Null(dashboard.ResolutionTime.MedianHours);
        Assert.Equal(0, Assert.Single(dashboard.AgentWorkload).ActiveTickets);
    }
}
