using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;
using OpsHub.Api.Data;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Tickets;

/// <summary>Small reference lists the ticket forms need (dropdown options).</summary>
[Route("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class LookupsController(OpsHubDbContext db) : ApiControllerBase
{
    /// <summary>Active categories, for the "new ticket" and "edit ticket" forms.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategorySummary>>> Categories(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategorySummary(c.Id, c.Name))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Active support agents with their current number of active tickets, for the
    /// assignment dropdown. Staff only.
    /// </summary>
    [HttpGet("agents")]
    [Authorize(Policy = AuthPolicies.StaffOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AgentSummary>>> Agents(CancellationToken cancellationToken) =>
        await (
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == AppRoles.SupportAgent && user.IsActive
            orderby user.DisplayName
            select new AgentSummary(
                user.Id,
                user.DisplayName,
                db.Tickets.Count(t => t.AssignedAgentId == user.Id &&
                    (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingForInformation))))
            .ToListAsync(cancellationToken);
}
