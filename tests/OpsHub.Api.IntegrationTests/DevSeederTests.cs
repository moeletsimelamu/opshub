using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

public class DevSeederTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeder_CreatesAllDemoRecords()
    {
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(DemoData.Users.Length, await db.Users.CountAsync(Ct));
            Assert.Equal(ReferenceDataSeeder.DefaultCategories.Length, await db.Categories.CountAsync(Ct));
            Assert.Equal(DemoData.Tickets.Length, await db.Tickets.CountAsync(Ct));
        });
    }

    [Fact]
    public async Task Seeder_PutsEveryUserInTheirRole()
    {
        await factory.WithDbAsync<object?>(async (_, services) =>
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            foreach (var demo in DemoData.Users)
            {
                var user = await userManager.FindByEmailAsync(DemoData.EmailFor(demo.Key));
                Assert.NotNull(user);
                Assert.Equal([demo.Role], await userManager.GetRolesAsync(user));
            }

            return null;
        });
    }

    [Fact]
    public async Task Seeder_StoresPasswordHashes_NotPlaintext()
    {
        await factory.WithDbAsync<object?>(async (db, services) =>
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var users = await db.Users.ToListAsync(Ct);

            Assert.All(users, u => Assert.DoesNotContain(factory.DemoPassword, u.PasswordHash, StringComparison.Ordinal));
            Assert.True(await userManager.CheckPasswordAsync(users[0], factory.DemoPassword));
            Assert.False(await userManager.CheckPasswordAsync(users[0], "wrong-password-123"));
            return null;
        });
    }

    [Fact]
    public async Task SeededTickets_HaveConsistentHistoryAndTimestamps()
    {
        await factory.WithDbAsync(async db =>
        {
            var tickets = await db.Tickets
                .Include(t => t.StatusHistory)
                .Include(t => t.Comments)
                .AsNoTracking()
                .ToListAsync(Ct);
            var now = DateTime.UtcNow;

            Assert.All(tickets, t =>
            {
                var history = t.StatusHistory.OrderBy(h => h.ChangedAt).ToList();

                // The timeline starts at Open and ends at the ticket's current status.
                Assert.Null(history[0].FromStatus);
                Assert.Equal(TicketStatus.Open, history[0].ToStatus);
                Assert.Equal(t.Status, history[^1].ToStatus);

                // Each change starts where the previous one ended.
                for (var i = 1; i < history.Count; i++)
                {
                    Assert.Equal(history[i - 1].ToStatus, history[i].FromStatus);
                }

                Assert.True(t.UpdatedAt >= t.CreatedAt);
                Assert.True(t.UpdatedAt <= now);
                Assert.All(t.Comments, c => Assert.InRange(c.CreatedAt, t.CreatedAt, t.UpdatedAt));

                if (t.Status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingForInformation)
                {
                    Assert.Null(t.ResolvedAt);
                }

                if (t.Status is TicketStatus.InProgress or TicketStatus.WaitingForInformation or TicketStatus.Resolved)
                {
                    Assert.NotNull(t.AssignedAgentId);
                }
            });
        });
    }

    [Fact]
    public async Task Seeder_RunningAgain_DoesNotDuplicateData()
    {
        var before = await factory.WithDbAsync((db, _) => db.Tickets.CountAsync(Ct));

        await factory.WithDbAsync<object?>(async (_, services) =>
        {
            await services.GetRequiredService<DevSeeder>().SeedAsync(Ct);
            return null;
        });

        var after = await factory.WithDbAsync((db, _) => db.Tickets.CountAsync(Ct));
        Assert.Equal(before, after);
    }
}

public class DevSeederWithoutPasswordTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task Seeder_WithoutDemoPassword_CreatesNothing()
    {
        var ct = TestContext.Current.CancellationToken;

        await factory.WithDbAsync<object?>(async (_, services) =>
        {
            await services.GetRequiredService<DevSeeder>().SeedAsync(ct);
            return null;
        });

        Assert.Equal(0, await factory.WithDbAsync((db, _) => db.Tickets.CountAsync(ct)));
        Assert.Equal(0, await factory.WithDbAsync((db, _) => db.Users.CountAsync(ct)));
    }
}
