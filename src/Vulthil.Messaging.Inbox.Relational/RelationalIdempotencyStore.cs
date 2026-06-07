using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// An <see cref="IIdempotencyStore"/> backed by a relational Entity Framework Core provider. It opens an ambient
/// database transaction so the consumer's own <c>SaveChanges</c> calls flush into it without committing; the
/// idempotency marker and the consumer's business writes then commit together, giving transactional exactly-once
/// processing.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/> type, which must expose the inbox set.</typeparam>
internal sealed class RelationalIdempotencyStore<TContext>(TContext dbContext, TimeProvider timeProvider)
    : IIdempotencyStore
    where TContext : DbContext, ISaveInboxMessages
{
    public async Task<IIdempotencyTransaction> BeginAsync(IMessageContext context, CancellationToken cancellationToken)
    {
        var currentTransaction = dbContext.Database.CurrentTransaction;
        var ownsTransaction = currentTransaction is null;
        var transaction = currentTransaction ?? await dbContext.Database.BeginTransactionAsync(cancellationToken);

        return new RelationalIdempotencyTransaction<TContext>(dbContext, transaction, ownsTransaction, timeProvider);
    }
}
