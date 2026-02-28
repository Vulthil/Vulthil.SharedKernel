using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Provider-agnostic base configuration for <see cref="OutboxMessage"/>.
/// Provider packages should supply their own <see cref="IEntityTypeConfiguration{OutboxMessage}"/>
/// with optimized indexes, column types, and filters (e.g., jsonb + filtered index for Postgres).
/// </summary>
public class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public virtual void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired();
        builder.Property(o => o.Content).IsRequired();
    }
}
