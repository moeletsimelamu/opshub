using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Data.Seeding;

/// <summary>
/// Fills an empty development database with the fictional data in <see cref="DemoData"/>.
/// Ticket times are relative to "now", so the demo always looks recent.
/// </summary>
internal sealed partial class DevSeeder(
    OpsHubDbContext db,
    UserManager<AppUser> userManager,
    TimeProvider timeProvider,
    IOptions<SeedOptions> options,
    ILogger<DevSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent: running the app twice must not duplicate the demo data.
        if (await db.Users.AnyAsync(cancellationToken))
        {
            LogAlreadySeeded(logger);
            return;
        }

        var password = options.Value.DemoPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            LogPasswordMissing(logger);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // One transaction: either all demo data is created or none of it is.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Roles and categories already exist: ReferenceDataSeeder runs first.
        var users = await SeedUsersAsync(password, now);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, cancellationToken);
        var tickets = DemoData.Tickets.Select(t => BuildTicket(t, users, categories, now)).ToList();
        db.Tickets.AddRange(tickets);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogSeeded(logger, users.Count, categories.Count, tickets.Count);
    }

    // Source-generated logging: the compiler writes the logging code, avoiding
    // boxing and string work when the log level is switched off.
    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding skipped: the database already contains users")]
    private static partial void LogAlreadySeeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seeding skipped: Seed:DemoPassword is not configured. " +
        "Set it with: dotnet user-secrets set \"Seed:DemoPassword\" \"<password>\" --project src/OpsHub.Api")]
    private static partial void LogPasswordMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {UserCount} users, {CategoryCount} categories and {TicketCount} tickets")]
    private static partial void LogSeeded(ILogger logger, int userCount, int categoryCount, int ticketCount);

    private async Task<Dictionary<string, AppUser>> SeedUsersAsync(string password, DateTime now)
    {
        var users = new Dictionary<string, AppUser>();

        foreach (var demo in DemoData.Users)
        {
            var email = DemoData.EmailFor(demo.Key);
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = demo.DisplayName,
                IsActive = demo.IsActive,
                CreatedAt = now.AddDays(-60),
            };

            // UserManager hashes the password with ASP.NET Core Identity's PasswordHasher
            // (PBKDF2 with a random salt). We never store or log the plaintext.
            EnsureSucceeded(await userManager.CreateAsync(user, password), $"create user {email}");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, demo.Role), $"add {email} to {demo.Role}");

            users[demo.Key] = user;
        }

        return users;
    }

    /// <summary>
    /// Builds a ticket together with a status history that follows a realistic path
    /// (Open → In Progress → …), so the timeline and the dashboard agree with each other.
    /// </summary>
    private static Ticket BuildTicket(
        DemoTicket demo,
        Dictionary<string, AppUser> users,
        Dictionary<string, Category> categories,
        DateTime now)
    {
        var requester = users[demo.RequesterKey];
        var agent = demo.AgentKey is null ? null : users[demo.AgentKey];
        var createdAt = now.AddDays(-demo.CreatedDaysAgo);

        var ticket = new Ticket
        {
            Title = demo.Title,
            Description = demo.Description,
            Category = categories.TryGetValue(demo.Category, out var category)
                ? category
                : throw new InvalidOperationException($"Demo ticket '{demo.Title}' uses category '{demo.Category}', which doesn't exist."),
            Priority = demo.Priority,
            Status = demo.Status,
            Requester = requester,
            AssignedAgent = agent,
            CreatedAt = createdAt,
        };

        void Move(TicketStatus? from, TicketStatus to, AppUser by, DateTime at, string? note = null) =>
            ticket.StatusHistory.Add(new TicketStatusHistory
            {
                FromStatus = from,
                ToStatus = to,
                ChangedBy = by,
                ChangedAt = at,
                Note = note,
            });

        Move(null, TicketStatus.Open, requester, createdAt);

        // The moment the agent last did work on the ticket; comments are placed after it.
        var lastWorkAt = createdAt;

        if (demo.Status != TicketStatus.Open)
        {
            var worker = agent ?? throw new InvalidOperationException($"Demo ticket '{demo.Title}' needs an agent.");

            if (demo.Status == TicketStatus.Closed && demo.HoursToResolve is null)
            {
                Move(TicketStatus.Open, TicketStatus.Closed, worker, createdAt.AddHours(2), demo.CloseNote);
            }
            else
            {
                var startedAt = createdAt.AddHours(Math.Min(1.5, (demo.HoursToResolve ?? 6) / 3));
                Move(TicketStatus.Open, TicketStatus.InProgress, worker, startedAt);
                lastWorkAt = startedAt;

                if (demo.Status == TicketStatus.WaitingForInformation)
                {
                    lastWorkAt = createdAt.AddHours(4);
                    Move(TicketStatus.InProgress, TicketStatus.WaitingForInformation, worker, lastWorkAt);
                }

                if (demo.Status is TicketStatus.Resolved or TicketStatus.Closed)
                {
                    var resolvedAt = createdAt.AddHours(demo.HoursToResolve
                        ?? throw new InvalidOperationException($"Demo ticket '{demo.Title}' needs HoursToResolve."));
                    Move(TicketStatus.InProgress, TicketStatus.Resolved, worker, resolvedAt);
                    ticket.ResolvedAt = resolvedAt;
                    lastWorkAt = resolvedAt;

                    if (demo.Status == TicketStatus.Closed)
                    {
                        Move(TicketStatus.Resolved, TicketStatus.Closed, worker, resolvedAt.AddHours(72),
                            "Closed after three days with no further issues reported.");
                    }
                }
            }
        }

        var comments = demo.Comments ?? [];
        for (var i = 0; i < comments.Length; i++)
        {
            ticket.Comments.Add(new TicketComment
            {
                Author = users[comments[i].AuthorKey],
                Body = comments[i].Body,
                IsInternal = comments[i].IsInternal,
                CreatedAt = lastWorkAt.AddMinutes(20 + (25 * i)),
            });
        }

        var lastActivity = ticket.StatusHistory.Select(h => h.ChangedAt)
            .Concat(ticket.Comments.Select(c => c.CreatedAt))
            .Max();

        if (lastActivity > now)
        {
            throw new InvalidOperationException($"Demo ticket '{demo.Title}' has activity in the future; adjust CreatedDaysAgo.");
        }

        ticket.UpdatedAt = lastActivity;
        return ticket;
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Seeding failed to {action}: {errors}");
        }
    }
}
