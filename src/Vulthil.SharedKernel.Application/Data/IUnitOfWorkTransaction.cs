namespace Vulthil.SharedKernel.Application.Data;

/// <summary>
/// Represents an active unit-of-work transaction that can be committed or rolled back.
/// </summary>
/// <remarks>
/// When the unit of work spans several databases, the transaction covers all of them, and a commit is atomic per
/// database only.
/// </remarks>
public interface IUnitOfWorkTransaction : IAsyncDisposable
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
