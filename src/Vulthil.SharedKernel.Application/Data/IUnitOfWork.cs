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
