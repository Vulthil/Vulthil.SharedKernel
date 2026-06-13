using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;

/// <summary>
/// Cosmos outbox store. Cosmos DB has no cross-partition transaction, so the relay runs without an ambient
/// transaction (best-effort): it uses the base LINQ fetch and materialized update. Consumers targeting Cosmos at
/// scale should supply an SDK-backed store.
/// </summary>
/// <typeparam name="TContext">The application's Cosmos <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class CosmosOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : EntityFrameworkOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    /// <inheritdoc />
    protected override Task<IDbTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IDbTransaction?>(new NoOpDbTransaction());
}

internal sealed class NoOpDbTransaction : IDbTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
