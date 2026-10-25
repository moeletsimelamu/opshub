using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;
using OpsHub.Api.Common.Results;
using OpsHub.Api.Data;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Tickets;

/// <summary>
/// Ticket use cases. The caller's identity always comes from <see cref="ICurrentUser"/>,
/// never from the request body, and every lookup starts from <see cref="TicketQueries.VisibleTo"/>.
/// </summary>
public sealed partial class TicketService(
    OpsHubDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<TicketService> logger)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private TicketActor Actor => currentUser.IsStaff ? TicketActor.Staff : TicketActor.Requester;

    // ---------- Queries ----------

    public async Task<PagedResult<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken cancellationToken)
    {
        var tickets = db.Tickets.AsNoTracking().VisibleTo(currentUser).ApplyFilters(query);

        var totalCount = await tickets.CountAsync(cancellationToken);
        var items = await tickets
            .ApplySort(query)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .SelectListItem()
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketListItem>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<Result<TicketDetails>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var details = await db.Tickets
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(t => t.Id == id)
            .Select(t => new TicketDetails(
                t.Id,
                Ticket.FormatReference(t.Id),
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                new CategorySummary(t.Category.Id, t.Category.Name),
                new PersonSummary(t.Requester.Id, t.Requester.DisplayName),
                t.AssignedAgent == null ? null : new PersonSummary(t.AssignedAgent.Id, t.AssignedAgent.DisplayName),
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                t.Version,
                null!))
            .FirstOrDefaultAsync(cancellationToken);

        // A ticket that doesn't exist and one the caller may not see get the same 404,
        // so requesters can't discover other people's ticket numbers.
        return details is null
            ? TicketNotFound()
            : details with { Permissions = PermissionsFor(details.Status, details.Requester.Id, details.AssignedAgent is not null) };
    }

    // ---------- Commands ----------

    public async Task<Result<TicketDetails>> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken))
        {
            return Error.Validation("categoryId", "Choose an active category.");
        }

        var now = Now;
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryId = request.CategoryId,
            Priority = request.Priority,
            Status = TicketStatus.Open,
            RequesterId = currentUser.Id, // Always the caller; a requesterId in the body is ignored.
            CreatedAt = now,
            UpdatedAt = now,
        };
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            FromStatus = null,
            ToStatus = TicketStatus.Open,
            ChangedById = currentUser.Id,
            ChangedAt = now,
        });

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        LogTicketCreated(logger, ticket.Id, currentUser.Id);

        return await GetAsync(ticket.Id, cancellationToken);
    }

    public async Task<Result<TicketDetails>> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await FindVisibleAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return TicketClosed();
        }

        if (!currentUser.IsStaff)
        {
            if (ticket.Status != TicketStatus.Open)
            {
                return Error.Conflict("ticket_locked",
                    "You can only edit your ticket while it is Open. Add a comment instead.");
            }

            if (request.Priority != ticket.Priority)
            {
                return Error.Forbidden("priority_change_requires_staff", "Only support staff can change the priority.");
            }
        }

        if (request.CategoryId != ticket.CategoryId &&
            !await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken))
        {
            return Error.Validation("categoryId", "Choose an active category.");
        }

        if (ticket.Version != request.Version)
        {
            return ConcurrencyConflict();
        }

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.CategoryId = request.CategoryId;
        ticket.Priority = request.Priority;
        ticket.UpdatedAt = Now;

        return await SaveAndReloadAsync(ticket, request.Version!.Value, cancellationToken);
    }

    public async Task<Result<TicketDetails>> AssignAsync(int id, AssignTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await FindVisibleAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return TicketClosed();
        }

        if (ticket.Version != request.Version)
        {
            return ConcurrencyConflict();
        }

        if (request.AgentId is null)
        {
            if (ticket.AssignedAgentId is null)
            {
                return await GetAsync(id, cancellationToken);
            }

            TicketWorkflow.ReturnToQueue(ticket, currentUser.Id, "Unassigned and returned to the queue.", Now);
        }
        else
        {
            if (!await IsActiveSupportAgentAsync(request.AgentId.Value, cancellationToken))
            {
                return Error.Validation("agentId", "Choose an active support agent.");
            }

            ticket.AssignedAgentId = request.AgentId;
            ticket.UpdatedAt = Now;
        }

        LogTicketAssigned(logger, ticket.Id, request.AgentId, currentUser.Id);
        return await SaveAndReloadAsync(ticket, request.Version!.Value, cancellationToken);
    }

    public async Task<Result<TicketDetails>> ChangeStatusAsync(int id, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var ticket = await FindVisibleAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        if (ticket.Version != request.Version)
        {
            return ConcurrencyConflict();
        }

        var from = ticket.Status;
        var violation = TicketWorkflow.ChangeStatus(ticket, request.Status!.Value, Actor, currentUser.Id, request.Note, Now);
        if (violation is not null)
        {
            return ToError(violation);
        }

        LogStatusChanged(logger, ticket.Id, from, ticket.Status, currentUser.Id);
        return await SaveAndReloadAsync(ticket, request.Version!.Value, cancellationToken);
    }

    // ---------- Shared helpers (also used by comments and user administration) ----------

    internal Task<Ticket?> FindVisibleAsync(int id, CancellationToken cancellationToken) =>
        db.Tickets.VisibleTo(currentUser).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <summary>
    /// When an agent is deactivated or stops being an agent, their unfinished tickets go
    /// back to the queue so nothing is silently stuck with someone who can't work on it.
    /// </summary>
    internal async Task<int> ReturnAgentTicketsToQueueAsync(Guid agentId, string reason, CancellationToken cancellationToken)
    {
        var tickets = await db.Tickets
            .Where(t => t.AssignedAgentId == agentId &&
                        (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingForInformation))
            .ToListAsync(cancellationToken);

        var now = Now;
        foreach (var ticket in tickets)
        {
            TicketWorkflow.ReturnToQueue(ticket, currentUser.Id, reason, now);
        }

        return tickets.Count;
    }

    internal TicketPermissions PermissionsFor(TicketStatus status, Guid requesterId, bool hasAssignee)
    {
        var isOpenForChanges = status != TicketStatus.Closed;
        var isOwner = requesterId == currentUser.Id;

        return new TicketPermissions(
            CanEdit: isOpenForChanges && (currentUser.IsStaff || (isOwner && status == TicketStatus.Open)),
            CanEditPriority: isOpenForChanges && currentUser.IsStaff,
            CanAssign: isOpenForChanges && currentUser.IsStaff,
            CanComment: isOpenForChanges,
            CanAddInternalComment: isOpenForChanges && currentUser.IsStaff,
            AllowedStatuses: TicketWorkflow.AllowedTargets(status, Actor, hasAssignee));
    }

    internal static Error TicketNotFound() => Error.NotFound("ticket_not_found", "Ticket not found.");

    internal static Error TicketClosed() => Error.Conflict("ticket_closed", "Closed tickets cannot be changed.");

    private static Error ConcurrencyConflict() => Error.Conflict(
        "concurrency_conflict", "Someone else changed this ticket while you were editing. Reload it and try again.");

    private static Error ToError(WorkflowViolation violation) => violation.Kind switch
    {
        WorkflowViolationKind.Forbidden => Error.Forbidden(violation.Code, violation.Message),
        WorkflowViolationKind.InputRequired => Error.Validation("note", violation.Message) with { Code = violation.Code },
        _ => Error.Conflict(violation.Code, violation.Message),
    };

    private async Task<bool> IsActiveSupportAgentAsync(Guid userId, CancellationToken cancellationToken) =>
        await (
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.Id == userId && user.IsActive && role.Name == AppRoles.SupportAgent
            select user.Id).AnyAsync(cancellationToken);

    /// <summary>
    /// Saves with optimistic concurrency: EF Core adds "WHERE Version = @expected" to the
    /// UPDATE. If another request saved in between (after our up-front check), zero rows
    /// match and EF Core throws, which becomes a 409 instead of a lost update.
    /// </summary>
    private async Task<Result<TicketDetails>> SaveAndReloadAsync(Ticket ticket, Guid expectedVersion, CancellationToken cancellationToken)
    {
        db.Entry(ticket).Property(t => t.Version).OriginalValue = expectedVersion;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        return await GetAsync(ticket.Id, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} created by {UserId}")]
    private static partial void LogTicketCreated(ILogger logger, int ticketId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} assigned to {AgentId} by {UserId}")]
    private static partial void LogTicketAssigned(ILogger logger, int ticketId, Guid? agentId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} moved from {FromStatus} to {ToStatus} by {UserId}")]
    private static partial void LogStatusChanged(ILogger logger, int ticketId, TicketStatus fromStatus, TicketStatus toStatus, Guid userId);
}
