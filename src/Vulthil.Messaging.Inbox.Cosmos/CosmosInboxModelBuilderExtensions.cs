using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Cosmos;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the Cosmos <see cref="InboxMessage"/> mapping.
/// </summary>
public static class CosmosInboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the Cosmos <see cref="InboxMessage"/> entity configuration: a self-contained marker document keyed
    /// and partitioned by the idempotency key in its own container. Call this from the application Cosmos
    /// <see cref="DbContext"/>'s <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyCosmosInbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyConfiguration(new CosmosInboxMessageEntityConfiguration());
    }
}
