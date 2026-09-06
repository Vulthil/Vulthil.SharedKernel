using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;

/// <summary>
/// MySQL-specific additions on top of the provider-agnostic <see cref="OutboxMessage"/> mapping, which
/// <c>ApplyMySqlOutbox</c> applies first: a bounded type column, <c>longtext</c> content, and a composite index over
/// the relay's pending-state and ordering columns.
/// </summary>
internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(o => o.Type).HasMaxLength(256);
        builder.Property(o => o.Content).HasColumnType("longtext");
        // MySQL lacks filtered indexes, so lead with the pending-state columns then the relay's (OccurredOnUtc, Id) ordering.
        builder.HasIndex(o => new { o.ProcessedOnUtc, o.FailedOnUtc, o.OccurredOnUtc, o.Id })
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
