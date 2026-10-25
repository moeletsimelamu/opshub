using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Data;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>
/// Proves that the rules written in the EF Core configuration really exist in the
/// migrated SQLite schema, i.e. the database itself rejects invalid data.
/// </summary>
public class DatabaseSchemaTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AllMigrationsAreApplied_AndModelHasNoUnmigratedChanges()
    {
        await factory.WithDbAsync(async db =>
        {
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));

            // Fails if someone changed an entity or configuration without adding a migration.
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }

    [Fact]
    public async Task InvalidPriority_IsRejectedByCheckConstraint()
    {
        await factory.WithDbAsync(async db =>
        {
            var ticket = await NewTicketAsync(db);
            ticket.Priority = (TicketPriority)9;
            db.Tickets.Add(ticket);

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            Assert.Contains("CK_Tickets_Priority", error.InnerException?.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ResolvedTicketWithoutResolvedAt_IsRejectedByCheckConstraint()
    {
        await factory.WithDbAsync(async db =>
        {
            var ticket = await NewTicketAsync(db);
            ticket.Status = TicketStatus.Resolved;
            ticket.ResolvedAt = null;
            db.Tickets.Add(ticket);

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            Assert.Contains("CK_Tickets_ResolvedAt", error.InnerException?.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ConcurrentEdits_SecondSaveFailsWithConcurrencyException()
    {
        var ticketId = await factory.WithDbAsync(async (db, _) =>
        {
            var ticket = await NewTicketAsync(db);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync(Ct);
            return ticket.Id;
        });

        // Two "users" load the same ticket in separate contexts.
        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<OpsHubDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<OpsHubDbContext>();

        var copyA = await dbA.Tickets.SingleAsync(t => t.Id == ticketId, Ct);
        var copyB = await dbB.Tickets.SingleAsync(t => t.Id == ticketId, Ct);

        copyA.Title = "Edited by A";
        await dbA.SaveChangesAsync(Ct);

        copyB.Title = "Edited by B";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task DateTimes_ComeBackAsUtc()
    {
        var ticketId = await factory.WithDbAsync(async (db, _) =>
        {
            var ticket = await NewTicketAsync(db);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync(Ct);
            return ticket.Id;
        });

        await factory.WithDbAsync(async db =>
        {
            var reloaded = await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId, Ct);
            Assert.Equal(DateTimeKind.Utc, reloaded.CreatedAt.Kind);
        });
    }

    /// <summary>Creates a valid, unsaved ticket together with the user and category it needs.</summary>
    private static async Task<Ticket> NewTicketAsync(OpsHubDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"schema-{suffix}@tests.example",
            NormalizedUserName = $"SCHEMA-{suffix}@TESTS.EXAMPLE",
            Email = $"schema-{suffix}@tests.example",
            NormalizedEmail = $"SCHEMA-{suffix}@TESTS.EXAMPLE",
            DisplayName = "Schema Test User",
        };
        var category = new Category { Name = $"Schema test {suffix[..8]}" };
        db.Users.Add(user);
        db.Categories.Add(category);
        await db.SaveChangesAsync(Ct);

        var now = DateTime.UtcNow;
        return new Ticket
        {
            Title = "Schema test ticket",
            Description = "Created by DatabaseSchemaTests.",
            CategoryId = category.Id,
            RequesterId = user.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
