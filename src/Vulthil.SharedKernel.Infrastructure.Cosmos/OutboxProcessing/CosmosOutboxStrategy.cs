using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;

/// <summary>
/// Cosmos outbox strategy - uses a simple relational query as a fallback. Consumers targeting Cosmos should implement an SDK-backed strategy for best performance.
/// </summary>
public class CosmosOutboxStrategy : BaseOutboxStrategy
{
    /// <inheritdoc />
    public override Task<IDbTransaction?> BeginTransactionAsync(ISaveOutboxMessages context, CancellationToken cancellationToken)
    {
        // Cosmos DB transactions are limited; do not return an ambient transaction.
        return Task.FromResult<IDbTransaction?>(new NoOpDbTransaction());
    }
}

internal class NoOpDbTransaction : IDbTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

}
