using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Tickets;

/// <summary>Reusable query building blocks for tickets. All of them run in the database.</summary>
internal static partial class TicketQueries
{
    /// <summary>
    /// THE visibility rule. Staff see every ticket; requesters see only their own.
    /// Every ticket read and write starts from this method, so a requester can never reach
    /// another person's ticket through the list, search, details, comments or history.
    /// </summary>
    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, ICurrentUser user)
    {
        if (user.IsStaff)
        {
            return tickets;
        }

        var userId = user.Id;
        return tickets.Where(t => t.RequesterId == userId);
    }

    public static IQueryable<Ticket> ApplyFilters(this IQueryable<Ticket> tickets, TicketListQuery query)
    {
        if (query.Status is { Count: > 0 } statuses)
        {
            tickets = tickets.Where(t => statuses.Contains(t.Status));
        }

        if (query.Priority is { Count: > 0 } priorities)
        {
            tickets = tickets.Where(t => priorities.Contains(t.Priority));
        }

        if (query.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(t => t.CategoryId == categoryId);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            tickets = tickets.Where(t => t.AssignedAgentId == assigneeId);
        }

        if (query.Unassigned == true)
        {
            tickets = tickets.Where(t => t.AssignedAgentId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var pattern = LikePattern.Contains(term);
            var escape = LikePattern.EscapeCharacter.ToString();
            var referenceId = ParseReference(term);

            tickets = tickets.Where(t =>
                EF.Functions.Like(t.Title, pattern, escape) ||
                EF.Functions.Like(t.Description, pattern, escape) ||
                (referenceId != null && t.Id == referenceId));
        }

        return tickets;
    }

    /// <summary>
    /// Sorts by a whitelisted column. Ticket ID is always the final tie-breaker, so the
    /// order is stable and paging never shows a row twice or skips one.
    /// </summary>
    public static IQueryable<Ticket> ApplySort(this IQueryable<Ticket> tickets, TicketListQuery query)
    {
        var descending = query.SortDirection != "asc";

        var sorted = query.SortBy switch
        {
            "createdAt" => Order(tickets, t => t.CreatedAt, descending),
            "priority" => Order(tickets, t => t.Priority, descending),
            "status" => Order(tickets, t => t.Status, descending),
            "title" => Order(tickets, t => EF.Functions.Collate(t.Title, "NOCASE"), descending),
            "id" => Order(tickets, t => t.Id, descending),
            _ => Order(tickets, t => t.UpdatedAt, descending),
        };

        return descending ? sorted.ThenByDescending(t => t.Id) : sorted.ThenBy(t => t.Id);
    }

    /// <summary>Projects straight to the DTO, so only the needed columns are read.</summary>
    public static IQueryable<TicketListItem> SelectListItem(this IQueryable<Ticket> tickets) =>
        tickets.Select(t => new TicketListItem(
            t.Id,
            Ticket.FormatReference(t.Id),
            t.Title,
            t.Status,
            t.Priority,
            new CategorySummary(t.Category.Id, t.Category.Name),
            new PersonSummary(t.Requester.Id, t.Requester.DisplayName),
            t.AssignedAgent == null ? null : new PersonSummary(t.AssignedAgent.Id, t.AssignedAgent.DisplayName),
            t.CreatedAt,
            t.UpdatedAt));

    /// <summary>"OPS-000012", "ops-12" or "12" → 12. Anything else → null.</summary>
    public static int? ParseReference(string term)
    {
        var match = ReferencePattern().Match(term);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    private static IOrderedQueryable<Ticket> Order<TKey>(IQueryable<Ticket> tickets, Expression<Func<Ticket, TKey>> key, bool descending) =>
        descending ? tickets.OrderByDescending(key) : tickets.OrderBy(key);

    [GeneratedRegex(@"^(?:OPS-)?0*(\d{1,9})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}
