using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Exposes the <see cref="OutboxMessage"/> entity set for persistence contexts that support outbox integration.
/// </summary>
public interface ISaveOutboxMessages
{
    /// <summary>
    /// Gets the <see cref="OutboxMessage"/> set tracked by the persistence context.
    /// Used by the outbox processor to read, update, and add outbox messages.
    /// </summary>
    DbSet<OutboxMessage> OutboxMessages { get; }

    /// <summary>
    /// Gets a value indicating whether the persistence context currently has an active database transaction. The
    /// transactional outbox uses this to decide whether an outgoing message should be captured into the outbox
    /// (atomically with the in-flight business changes) or published directly.
    /// </summary>
    bool IsInTransaction { get; }

    /// <summary>
    /// Persists the pending outbox-message changes tracked by the context to the database.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
