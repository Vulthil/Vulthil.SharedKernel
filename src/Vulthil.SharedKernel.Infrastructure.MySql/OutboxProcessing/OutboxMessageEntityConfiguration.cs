using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;

/// <summary>
/// MySQL-specific OutboxMessage mapping.
/// </summary>
public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired().HasMaxLength(256);
        builder.Property(o => o.Content).IsRequired().HasColumnType("longtext");
        builder.Property(o => o.OccurredOnUtc).IsRequired();

        builder.HasIndex(o => new { o.OccurredOnUtc, o.ProcessedOnUtc })
            .HasDatabaseName("IX_OutboxMessages_OccurredOnUtc_ProcessedOnUtc");
    }
}
