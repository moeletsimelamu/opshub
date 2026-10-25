using System.Net;
using OpsHub.Api.Common;
using OpsHub.Api.Features.Auth;
using OpsHub.Api.Features.Tickets;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.IntegrationTests.Infrastructure;

/// <summary>Small helpers so ticket tests read as "what happens" rather than HTTP plumbing.</summary>
public static class TicketApi
{
    public static async Task<CurrentUserResponse> MeAsync(this HttpClient client)
    {
        using var response = await client.GetPathAsync("/api/auth/me");
        return await response.ReadJsonAsync<CurrentUserResponse>();
    }

    public static async Task<int> FirstCategoryIdAsync(this HttpClient client)
    {
        using var response = await client.GetPathAsync("/api/categories");
        return (await response.ReadJsonAsync<List<CategorySummary>>())[0].Id;
    }

    public static async Task<TicketDetails> CreateTicketAsync(
        this HttpClient client,
        string? title = null,
        TicketPriority priority = TicketPriority.Medium)
    {
        var request = new CreateTicketRequest
        {
            Title = title ?? $"Test ticket {Guid.NewGuid():N}",
            Description = "Created by an integration test.",
            CategoryId = await client.FirstCategoryIdAsync(),
            Priority = priority,
        };

        using var response = await client.PostJsonAsync("/api/tickets", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetails>();
    }

    public static async Task<TicketDetails> GetTicketAsync(this HttpClient client, int id)
    {
        using var response = await client.GetPathAsync($"/api/tickets/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetails>();
    }

    public static async Task<PagedResult<TicketListItem>> ListTicketsAsync(this HttpClient client, string queryString = "")
    {
        using var response = await client.GetPathAsync($"/api/tickets{queryString}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<PagedResult<TicketListItem>>();
    }

    public static Task<HttpResponseMessage> AssignAsync(this HttpClient client, TicketDetails ticket, Guid? agentId) =>
        client.PutJsonAsync($"/api/tickets/{ticket.Id}/assignee", new AssignTicketRequest { AgentId = agentId, Version = ticket.Version });

    public static Task<HttpResponseMessage> ChangeStatusAsync(this HttpClient client, TicketDetails ticket, TicketStatus status, string? note = null) =>
        client.PutJsonAsync($"/api/tickets/{ticket.Id}/status", new ChangeStatusRequest { Status = status, Note = note, Version = ticket.Version });

    public static Task<HttpResponseMessage> CommentAsync(this HttpClient client, int ticketId, string body, bool isInternal = false) =>
        client.PostJsonAsync($"/api/tickets/{ticketId}/comments", new AddCommentRequest { Body = body, IsInternal = isInternal });

    /// <summary>Sends a request that must succeed and returns the updated ticket.</summary>
    public static async Task<TicketDetails> ExpectTicketAsync(this Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return await response.ReadJsonAsync<TicketDetails>();
    }
}
