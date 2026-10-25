using System.Net;
using Microsoft.AspNetCore.Mvc;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

public class HttpsOnlyApiTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task ApiOverPlainHttp_IsRefusedWithAClearError_NotA500()
    {
        using var client = factory.CreateDefaultClient(new Uri("http://localhost"));

        // The first step of signing in: before this check it failed with a 500.
        using var response = await client.GetPathAsync("/api/auth/antiforgery");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("HTTPS is required", (await response.ReadJsonAsync<ProblemDetails>()).Title);
    }

    [Fact]
    public async Task HealthOverPlainHttp_StillAnswers_ForPlatformProbes()
    {
        using var client = factory.CreateDefaultClient(new Uri("http://localhost"));

        using var response = await client.GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
