using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Cosmos;

/// <summary>
/// Default Cosmos mapping for <see cref="InboxMessage"/>: a self-contained marker document keyed and partitioned
/// by <see cref="InboxMessage.MessageId"/> in its own container, so duplicate inserts conflict and point lookups
/// stay cheap. Consumers may override the container name or partitioning in their <see cref="DbContext"/>.
/// </summary>
public sealed class CosmosInboxMessageEntityConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToContainer("InboxMessages");
        builder.HasNoDiscriminator();
        builder.HasKey(x => x.MessageId);
        builder.HasPartitionKey(x => x.MessageId);
    }
}
