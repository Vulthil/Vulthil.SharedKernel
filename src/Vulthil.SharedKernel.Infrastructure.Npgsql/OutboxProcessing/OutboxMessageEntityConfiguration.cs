using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// PostgreSQL-specific OutboxMessage mapping. Stores Content as jsonb and adds helpful indexes.
/// </summary>
public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired();
        builder.Property(o => o.Content).IsRequired().HasColumnType("jsonb");
        builder.Property(o => o.OccurredOnUtc).IsRequired();

        // Index to efficiently query pending messages (neither processed nor dead-lettered)
        builder.HasIndex(o => new { o.OccurredOnUtc, o.Id })
            .HasDatabaseName("IX_OutboxMessages_OccurredOnUtc_Id")
            .HasFilter("\"ProcessedOnUtc\" IS NULL AND \"FailedOnUtc\" IS NULL");
    }
}
