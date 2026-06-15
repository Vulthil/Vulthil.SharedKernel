using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// Provider-agnostic <see cref="OutboxMessage"/> mapping (primary key and required columns only). Applied via the
/// <c>ApplyOutbox</c> model-builder extension; provider packages supply optimized configurations with
/// provider-specific column types, indexes, and filters.
/// </summary>
internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).IsRequired();
        builder.Property(o => o.Content).IsRequired();
    }
}
