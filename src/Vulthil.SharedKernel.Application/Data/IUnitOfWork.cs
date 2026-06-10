namespace Vulthil.SharedKernel.Application.Data;

/// <summary>
/// Abstracts the persistence boundary for saving changes and managing transactions.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes to the underlying store.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A transaction that can be committed or rolled back.</returns>
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction, committing on success and rolling back on
    /// failure, using the store's execution strategy so it is compatible with a retrying execution strategy.
    /// </summary>
    /// <remarks>
    /// Prefer this over manually pairing <see cref="BeginTransactionAsync"/> with a commit when a retrying execution
    /// strategy may be configured (e.g. the EF Core default): a bare user-initiated transaction is rejected under a
    /// retrying strategy, whereas this wraps the whole begin/operation/commit as one retriable unit. A transient-fault
    /// retry re-runs <paramref name="operation"/> from a clean change-tracker state, so it must be idempotent. If a
    /// transaction is already active, <paramref name="operation"/> simply joins it (the outer scope owns the commit),
    /// so this composes with an outer caller that already opened a transaction.
    /// </remarks>
    /// <typeparam name="TResult">The type produced by <paramref name="operation"/>.</typeparam>
    /// <param name="operation">The work to run inside the transaction.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The result produced by <paramref name="operation"/>.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an active database transaction that can be committed or rolled back.
/// </summary>
public interface IDbTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction, persisting all changes.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task CommitAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Rolls back the transaction, discarding all changes.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
