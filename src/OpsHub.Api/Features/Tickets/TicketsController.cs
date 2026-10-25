using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;

namespace OpsHub.Api.Features.Tickets;

/// <summary>
/// Tickets for every signed-in user. Requesters see and act on their own tickets only;
/// that rule is enforced in the services, not here.
/// </summary>
[Route("api/tickets")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class TicketsController(TicketService tickets, TicketActivityService activity) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TicketListItem>>> List([FromQuery] TicketListQuery query, CancellationToken cancellationToken) =>
        Ok(await tickets.ListAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetails>> Get(int id, CancellationToken cancellationToken) =>
        ToActionResult(await tickets.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDetails>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await tickets.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : Problem(result.Error);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketDetails>> Update(int id, UpdateTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await tickets.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/assignee")]
    [Authorize(Policy = AuthPolicies.StaffOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketDetails>> Assign(int id, AssignTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await tickets.AssignAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketDetails>> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await tickets.ChangeStatusAsync(id, request, cancellationToken));

    [HttpGet("{id:int}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> GetComments(int id, CancellationToken cancellationToken) =>
        ToActionResult(await activity.GetCommentsAsync(id, cancellationToken));

    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommentResponse>> AddComment(int id, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await activity.AddCommentAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetComments), new { id }, result.Value)
            : Problem(result.Error);
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StatusHistoryResponse>>> GetHistory(int id, CancellationToken cancellationToken) =>
        ToActionResult(await activity.GetHistoryAsync(id, cancellationToken));
}
