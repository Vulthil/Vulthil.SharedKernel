using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// PostgreSQL-specific additions on top of the provider-agnostic <see cref="OutboxMessage"/> mapping, which
/// <c>ApplyNpgsqlOutbox</c> applies first: the content column is stored as <c>jsonb</c>, and a filtered index over
/// <c>(OccurredOnUtc, Id)</c> serves the relay's pending-message query (rows neither processed nor dead-lettered).
/// </summary>
internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(o => o.Content).HasColumnType("jsonb");
        builder.HasIndex(o => new { o.OccurredOnUtc, o.Id })
            .HasDatabaseName("IX_OutboxMessages_OccurredOnUtc_Id")
            .HasFilter("\"ProcessedOnUtc\" IS NULL AND \"FailedOnUtc\" IS NULL");
    }
}
