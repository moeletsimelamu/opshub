using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace OpsHub.Api.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email);

/// <summary>Does what Angular's HttpClient does: send the XSRF cookie back as a header.</summary>
internal sealed class XsrfHeaderHandler(CookieContainer cookies) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isSafeMethod = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        if (!isSafeMethod && request.RequestUri is not null)
        {
            var token = cookies.GetCookies(request.RequestUri)["XSRF-TOKEN"]?.Value;
            if (token is not null)
            {
                request.Headers.Add("X-XSRF-TOKEN", token);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public static class BrowserClientExtensions
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Fetches an XSRF token (as the SPA does at startup), then posts the login form.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password)
    {
        using var tokenResponse = await client.GetAsync(new Uri("/api/auth/antiforgery", UriKind.Relative), Ct);
        tokenResponse.EnsureSuccessStatusCode();

        return await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, password }, Ct);
    }

    public static Task<HttpResponseMessage> GetPathAsync(this HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PutAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Ct)
        ?? throw new InvalidOperationException("Response body was empty.");

    /// <summary>Asserts the status code and that the body is RFC 9457 ProblemDetails.</summary>
    public static async Task<ValidationProblemDetails> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.ReadJsonAsync<ValidationProblemDetails>();
    }

    /// <summary>The machine-readable "code" extension that OpsHub adds to business-rule errors.</summary>
    public static string? Code(this ProblemDetails problem) =>
        problem.Extensions.TryGetValue("code", out var code) ? code?.ToString() : null;
}
