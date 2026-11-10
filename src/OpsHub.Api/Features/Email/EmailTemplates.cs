using System.Text;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Email;

/// <summary>
/// The wording of every email OpsHub sends, in one place. Plain text only: ticket titles and
/// replies are user input, and plain text can't turn them into markup or links.
/// </summary>
internal static class EmailTemplates
{
    private const int ExcerptLength = 500;

    public static EmailMessage PasswordReset(string to, string name, string link) => new(
        to, name,
        "Reset your OpsHub password",
        Body(name,
            "Someone asked to reset the password for your OpsHub account. If it was you, choose a new",
            "password here. The link works once, for one hour:",
            "",
            link,
            "",
            "If it wasn't you, ignore this email: your password stays the same."),
        Kind: "password-reset");

    public static EmailMessage ReplyToRequester(TicketContact ticket, string authorName, string reply, string link) => new(
        ticket.Requester.Email, ticket.Requester.Name,
        Subject(ticket, "New reply"),
        Body(ticket.Requester.Name,
            $"{authorName} replied to your ticket {ticket.Reference}, \"{ticket.Title}\":",
            "",
            Excerpt(reply),
            "",
            $"Reply or follow the ticket: {link}"),
        Kind: "reply", ticket.Reference);

    public static EmailMessage RequesterReplied(TicketContact ticket, Person agent, string reply, string link) => new(
        agent.Email, agent.Name,
        Subject(ticket, "The requester replied"),
        Body(agent.Name,
            $"{ticket.Requester.Name} replied to {ticket.Reference}, \"{ticket.Title}\", which is assigned to you:",
            "",
            Excerpt(reply),
            "",
            $"Open the ticket: {link}"),
        Kind: "requester-replied", ticket.Reference);

    public static EmailMessage StatusForRequester(TicketContact ticket, TicketStatus status, string? note, string link)
    {
        var (subject, sentence) = status switch
        {
            TicketStatus.WaitingForInformation => ("We need more information", "The support team needs more information before they can continue."),
            TicketStatus.Resolved => ("Resolved: please confirm the fix", "The support team has resolved your ticket. Please confirm the fix, or reopen the ticket if the problem continues."),
            _ => ("Closed", "Your ticket has been closed."),
        };

        return new EmailMessage(
            ticket.Requester.Email, ticket.Requester.Name,
            Subject(ticket, subject),
            Body(ticket.Requester.Name,
                $"{ticket.Reference}, \"{ticket.Title}\": {sentence}",
                note is null ? "" : $"\nThe support team wrote:\n{Excerpt(note)}",
                "",
                $"Open the ticket: {link}"),
            Kind: $"status-{status}", ticket.Reference);
    }

    public static EmailMessage Reopened(TicketContact ticket, Person agent, string? note, string link) => new(
        agent.Email, agent.Name,
        Subject(ticket, "Reopened by the requester"),
        Body(agent.Name,
            $"{ticket.Requester.Name} reopened {ticket.Reference}, \"{ticket.Title}\": the problem isn't fixed.",
            note is null ? "" : $"\nThey wrote:\n{Excerpt(note)}",
            "",
            $"Open the ticket: {link}"),
        Kind: "reopened", ticket.Reference);

    public static EmailMessage AssignedToAgent(TicketContact ticket, Person agent, string assignedBy, string link) => new(
        agent.Email, agent.Name,
        Subject(ticket, "Assigned to you"),
        Body(agent.Name,
            $"{assignedBy} assigned {ticket.Reference}, \"{ticket.Title}\", to you.",
            "",
            $"Open the ticket: {link}"),
        Kind: "assigned", ticket.Reference);

    // "[OPS-000012] New reply: Printer offline". Control characters (line breaks above all)
    // are removed, so a title can't add lines to the email's headers.
    private static string Subject(TicketContact ticket, string what)
    {
        var title = new string(ticket.Title.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        if (title.Length > 80)
        {
            title = title[..79] + "…";
        }

        return $"[{ticket.Reference}] {what}: {title}";
    }

    private static string Excerpt(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= ExcerptLength ? trimmed : trimmed[..ExcerptLength] + "…";
    }

    private static string Body(string name, params string[] lines)
    {
        var body = new StringBuilder();
        body.Append("Hello ").Append(name).AppendLine(",").AppendLine();
        foreach (var line in lines)
        {
            body.AppendLine(line);
        }

        body.AppendLine().AppendLine("This is an automatic message from OpsHub, Marula Logistics' IT service desk.");
        return body.ToString();
    }
}

/// <summary>Someone an email can go to.</summary>
internal sealed record Person(Guid Id, string Name, string Email, bool IsActive);

/// <summary>What a ticket email needs to know about the ticket and its people.</summary>
internal sealed record TicketContact(int Id, string Title, Person Requester, Person? Agent)
{
    public string Reference => Ticket.FormatReference(Id);
}
