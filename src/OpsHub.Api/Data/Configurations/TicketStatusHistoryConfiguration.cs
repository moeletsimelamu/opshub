using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Data.Configurations;

internal sealed class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        builder.ToTable("TicketStatusHistory", table =>
        {
            table.HasCheckConstraint("CK_TicketStatusHistory_ToStatus", "\"ToStatus\" BETWEEN 1 AND 5");
            table.HasCheckConstraint(
                "CK_TicketStatusHistory_FromStatus",
                "\"FromStatus\" IS NULL OR \"FromStatus\" BETWEEN 1 AND 5");
        });

        builder.Property(h => h.Note).HasMaxLength(TicketStatusHistory.NoteMaxLength);

        builder.HasOne(h => h.Ticket)
            .WithMany(t => t.StatusHistory)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.TicketId, h.ChangedAt });
    }
}
