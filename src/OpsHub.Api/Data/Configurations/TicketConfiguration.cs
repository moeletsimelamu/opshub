using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Data.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", table =>
        {
            // Database-level guards: even a bug in the API cannot store an invalid value.
            table.HasCheckConstraint("CK_Tickets_Priority", "\"Priority\" BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_Tickets_Status", "\"Status\" BETWEEN 1 AND 5");

            // Active tickets have no resolution time; Resolved tickets must have one;
            // Closed tickets may or may not (a ticket can be closed without being resolved).
            table.HasCheckConstraint(
                "CK_Tickets_ResolvedAt",
                "(\"Status\" IN (1, 2, 3) AND \"ResolvedAt\" IS NULL) OR (\"Status\" = 4 AND \"ResolvedAt\" IS NOT NULL) OR \"Status\" = 5");
        });

        builder.Ignore(t => t.Reference);

        builder.Property(t => t.Title).HasMaxLength(Ticket.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.Version).IsConcurrencyToken();

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Requester)
            .WithMany()
            .HasForeignKey(t => t.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedAgent)
            .WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes match the filters and sorts the ticket list will use.
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.UpdatedAt);
        builder.HasIndex(t => t.RequesterId);
        builder.HasIndex(t => t.AssignedAgentId);
    }
}
