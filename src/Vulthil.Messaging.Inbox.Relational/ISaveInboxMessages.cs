using Microsoft.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// Exposes the <see cref="InboxMessage"/> set for persistence contexts that support inbox-based idempotency.
/// Implement this on the application's <see cref="DbContext"/> so the idempotency store can read and write markers.
/// </summary>
public interface ISaveInboxMessages
{
    /// <summary>
    /// Gets the <see cref="InboxMessage"/> set tracked by the persistence context.
    /// </summary>
    DbSet<InboxMessage> InboxMessages { get; }

    /// <summary>
    /// Persists all pending changes to the underlying store.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
