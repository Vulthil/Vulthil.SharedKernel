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
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction, committing only when
    /// <paramref name="shouldCommit"/> returns <see langword="true"/> for the produced result and rolling back
    /// otherwise (or when <paramref name="operation"/> throws), using the store's execution strategy.
    /// </summary>
    /// <remarks>
    /// Use this overload to roll back on a <em>returned</em> failure — for example a failed
    /// <see cref="Vulthil.Results.Result"/> from a command handler — rather than only on a thrown exception. The same
    /// execution-strategy and ambient-transaction semantics as
    /// <see cref="ExecuteInTransactionAsync{TResult}(System.Func{System.Threading.CancellationToken, System.Threading.Tasks.Task{TResult}}, System.Threading.CancellationToken)"/>
    /// apply; when a transaction is already active the outer scope owns the commit and <paramref name="shouldCommit"/>
    /// is not consulted.
    /// </remarks>
    /// <typeparam name="TResult">The type produced by <paramref name="operation"/>.</typeparam>
    /// <param name="operation">The work to run inside the transaction.</param>
    /// <param name="shouldCommit">A predicate that decides, from the produced result, whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The result produced by <paramref name="operation"/>.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, Func<TResult, bool> shouldCommit, CancellationToken cancellationToken = default);
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
