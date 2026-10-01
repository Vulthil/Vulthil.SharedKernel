using Microsoft.EntityFrameworkCore;
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
public class CosmosOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider)
    : EntityFrameworkOutboxStore<TContext>(dbContext, timeProvider)
    where TContext : DbContext, ISaveOutboxMessages
{
    /// <summary>
    /// Returns <see langword="null"/>: Cosmos DB has no relay-wide transaction, and the base relay unit treats a
    /// <see langword="null"/> transaction as running without one.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>Always <see langword="null"/>.</returns>
    protected override Task<IDbTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IDbTransaction?>(null);
}
