using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;

/// <summary>
/// Cosmos-specific OutboxMessage mapping; provider-agnostic fallback mapping. Consumers may override in their DbContext.
/// </summary>
public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Provider-agnostic mapping. Consumers targeting CosmosDB can add container/partition configuration in their DbContext.
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired();
        builder.Property(o => o.Content).IsRequired();
        builder.Property(o => o.OccurredOnUtc).IsRequired();
    }
}
