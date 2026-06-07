using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// Default relational mapping for <see cref="InboxMessage"/>. The <see cref="InboxMessage.MessageId"/> is the
/// primary key, giving the uniqueness guarantee the idempotency store relies on. Consumers may override this
/// mapping in their <see cref="DbContext"/>.
/// </summary>
public sealed class InboxMessageEntityConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.MessageId);
        builder.Property(x => x.MessageId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ProcessedOnUtc).IsRequired();
    }
}
