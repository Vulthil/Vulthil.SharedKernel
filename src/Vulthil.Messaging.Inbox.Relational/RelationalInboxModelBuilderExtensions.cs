using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the relational <see cref="InboxMessage"/> mapping.
/// </summary>
public static class RelationalInboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the relational <see cref="InboxMessage"/> entity configuration, mapping the idempotency key as the
    /// primary key (which gives the uniqueness guarantee the idempotency store relies on). Call this from the
    /// application <see cref="DbContext"/>'s <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyRelationalInbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyConfiguration(new InboxMessageEntityConfiguration());
    }
}
