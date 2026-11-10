using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Data.Configurations;

internal sealed class TicketAssignmentHistoryConfiguration : IEntityTypeConfiguration<TicketAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<TicketAssignmentHistory> builder)
    {
        builder.ToTable("TicketAssignmentHistory", table =>
            // A row records a change: from one agent to another, or to/from nobody.
            table.HasCheckConstraint(
                "CK_TicketAssignmentHistory_Changes",
                "\"FromAgentId\" IS NOT NULL OR \"ToAgentId\" IS NOT NULL"));

        builder.Property(h => h.Note).HasMaxLength(TicketAssignmentHistory.NoteMaxLength);

        builder.HasOne(h => h.Ticket)
            .WithMany(t => t.AssignmentHistory)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // People are never deleted (they are deactivated), so history keeps pointing at them.
        builder.HasOne(h => h.FromAgent).WithMany().HasForeignKey(h => h.FromAgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.ToAgent).WithMany().HasForeignKey(h => h.ToAgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.ChangedBy).WithMany().HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.TicketId, h.ChangedAt });
    }
}
