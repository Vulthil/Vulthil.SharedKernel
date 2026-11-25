using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

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
    /// Persists the pending outbox-message changes tracked by the context to the database.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
