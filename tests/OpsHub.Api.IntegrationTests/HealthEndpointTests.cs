using System.Net;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

public class HealthEndpointTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task Health_ReturnsHealthy_WhenDatabaseIsReachable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnknownRoute_Anonymous_Returns401ProblemDetails()
    {
        // The fallback "must be signed in" policy also covers URLs that match no endpoint,
        // so anonymous visitors cannot probe which routes exist.
        using var client = factory.CreateClient();

        using var response = await client.GetPathAsync("/api/does-not-exist");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }
}
