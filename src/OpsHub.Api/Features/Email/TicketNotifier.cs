using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsHub.Api.Auth;
using OpsHub.Api.Data;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Email;

/// <summary>
/// Decides who hears about a ticket change by email, and queues the emails. Called only after a
/// change has been saved, so a failed or conflicting change never sends anything.
/// Rules: nobody is emailed about their own action; deactivated people are never emailed;
/// internal notes are never emailed.
/// </summary>
public sealed class TicketNotifier(OpsHubDbContext db, EmailOutbox queue, IOptions<EmailOptions> options, ICurrentUser currentUser)
{
    public async Task CommentAddedAsync(int ticketId, string body, bool isInternal, CancellationToken cancellationToken)
    {
        if (!queue.Enabled || isInternal)
        {
            return;
        }

        var ticket = await LoadAsync(ticketId, cancellationToken);
        if (currentUser.Id == ticket.Requester.Id)
        {
            if (Reachable(ticket.Agent) is { } agent)
            {
                queue.Enqueue(EmailTemplates.RequesterReplied(ticket, agent, body, Link(ticket)));
            }
        }
        else if (Reachable(ticket.Requester) is not null)
        {
            queue.Enqueue(EmailTemplates.ReplyToRequester(ticket, await CurrentUserNameAsync(cancellationToken), body, Link(ticket)));
        }
    }

    public async Task StatusChangedAsync(int ticketId, TicketStatus from, TicketStatus to, string? note, CancellationToken cancellationToken)
    {
        if (!queue.Enabled)
        {
            return;
        }

        var ticket = await LoadAsync(ticketId, cancellationToken);
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (currentUser.Id == ticket.Requester.Id)
        {
            // The requester reopened a resolved ticket: the agent needs to know.
            if (from == TicketStatus.Resolved && to == TicketStatus.Open && Reachable(ticket.Agent) is { } agent)
            {
                queue.Enqueue(EmailTemplates.Reopened(ticket, agent, trimmedNote, Link(ticket)));
            }
        }
        else if (to is TicketStatus.WaitingForInformation or TicketStatus.Resolved or TicketStatus.Closed
                 && Reachable(ticket.Requester) is not null)
        {
            queue.Enqueue(EmailTemplates.StatusForRequester(ticket, to, trimmedNote, Link(ticket)));
        }
    }

    public async Task AssignedAsync(int ticketId, Guid agentId, CancellationToken cancellationToken)
    {
        if (!queue.Enabled || agentId == currentUser.Id) // picking a ticket up yourself needs no email
        {
            return;
        }

        var ticket = await LoadAsync(ticketId, cancellationToken);
        if (Reachable(ticket.Agent) is { } agent && agent.Id == agentId)
        {
            queue.Enqueue(EmailTemplates.AssignedToAgent(ticket, agent, await CurrentUserNameAsync(cancellationToken), Link(ticket)));
        }
    }

    // Active, and not the person who made the change.
    private Person? Reachable(Person? person) => person is { IsActive: true } && person.Id != currentUser.Id ? person : null;

    private string Link(TicketContact ticket) => $"{options.Value.PublicBaseUrl!.TrimEnd('/')}/tickets/{ticket.Id}";

    private Task<TicketContact> LoadAsync(int ticketId, CancellationToken cancellationToken) =>
        db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new TicketContact(
                t.Id,
                t.Title,
                new Person(t.Requester.Id, t.Requester.DisplayName, t.Requester.Email!, t.Requester.IsActive),
                t.AssignedAgent == null ? null
                    : new Person(t.AssignedAgent.Id, t.AssignedAgent.DisplayName, t.AssignedAgent.Email!, t.AssignedAgent.IsActive)))
            .SingleAsync(cancellationToken);

    private Task<string> CurrentUserNameAsync(CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == currentUser.Id).Select(u => u.DisplayName).SingleAsync(cancellationToken);
}
