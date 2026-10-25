using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Common.Results;
using OpsHub.Api.Data;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Tickets;

/// <summary>Comments and status history: the ticket's timeline.</summary>
public sealed class TicketActivityService(
    OpsHubDbContext db,
    TicketService tickets,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<CommentResponse>>> GetCommentsAsync(int ticketId, CancellationToken cancellationToken)
    {
        if (!await db.Tickets.VisibleTo(currentUser).AnyAsync(t => t.Id == ticketId, cancellationToken))
        {
            return TicketService.TicketNotFound();
        }

        var staffRoleIds = await StaffRoleIdsAsync(cancellationToken);
        var includeInternal = currentUser.IsStaff;

        var comments = await db.TicketComments
            .AsNoTracking()
            .Where(c => c.TicketId == ticketId && (includeInternal || !c.IsInternal)) // filtered in SQL, never sent to requesters
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new CommentResponse(
                c.Id,
                c.Body,
                c.IsInternal,
                new PersonSummary(c.Author.Id, c.Author.DisplayName),
                db.UserRoles.Any(ur => ur.UserId == c.AuthorId && staffRoleIds.Contains(ur.RoleId)),
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return comments;
    }

    public async Task<Result<CommentResponse>> AddCommentAsync(int ticketId, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindVisibleAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketService.TicketNotFound();
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return TicketService.TicketClosed();
        }

        if (request.IsInternal && !currentUser.IsStaff)
        {
            return Error.Forbidden("internal_comments_staff_only", "Only support staff can add internal notes.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            AuthorId = currentUser.Id,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = now,
        };
        db.TicketComments.Add(comment);
        ticket.UpdatedAt = now;

        if (!currentUser.IsStaff && ticket.RequesterId == currentUser.Id)
        {
            TicketWorkflow.ResumeAfterRequesterReply(ticket, currentUser.Id, now);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("concurrency_conflict", "The ticket changed while you were commenting. Reload it and try again.");
        }

        var author = await db.Users.AsNoTracking()
            .Where(u => u.Id == currentUser.Id)
            .Select(u => new PersonSummary(u.Id, u.DisplayName))
            .SingleAsync(cancellationToken);

        return new CommentResponse(comment.Id, comment.Body, comment.IsInternal, author, currentUser.IsStaff, comment.CreatedAt);
    }

    public async Task<Result<IReadOnlyList<StatusHistoryResponse>>> GetHistoryAsync(int ticketId, CancellationToken cancellationToken)
    {
        if (!await db.Tickets.VisibleTo(currentUser).AnyAsync(t => t.Id == ticketId, cancellationToken))
        {
            return TicketService.TicketNotFound();
        }

        var history = await db.TicketStatusHistory
            .AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new StatusHistoryResponse(
                h.Id,
                h.FromStatus,
                h.ToStatus,
                new PersonSummary(h.ChangedBy.Id, h.ChangedBy.DisplayName),
                h.Note,
                h.ChangedAt))
            .ToListAsync(cancellationToken);

        return history;
    }

    private Task<List<Guid>> StaffRoleIdsAsync(CancellationToken cancellationToken) =>
        db.Roles
            .Where(r => r.Name == AppRoles.Administrator || r.Name == AppRoles.SupportAgent)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
}
