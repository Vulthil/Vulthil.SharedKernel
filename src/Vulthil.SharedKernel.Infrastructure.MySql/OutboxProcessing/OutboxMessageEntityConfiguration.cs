using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;

/// <summary>
/// MySQL-specific OutboxMessage mapping.
/// </summary>
internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired().HasMaxLength(256);
        builder.Property(o => o.Content).IsRequired().HasColumnType("longtext");
        builder.Property(o => o.OccurredOnUtc).IsRequired();

        // MySQL lacks filtered indexes, so lead with the pending-state columns then the relay's (OccurredOnUtc, Id) ordering.
        builder.HasIndex(o => new { o.ProcessedOnUtc, o.FailedOnUtc, o.OccurredOnUtc, o.Id })
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
