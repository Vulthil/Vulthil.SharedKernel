using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

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
}
