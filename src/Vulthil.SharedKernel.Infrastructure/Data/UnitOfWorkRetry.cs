using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// The transaction loop and retry rule every unit of work in this package shares: a retry runs the operation from a
/// clean change tracker, so it cannot repeat changes made before the call and throws instead of dropping them.
/// </summary>
internal static class UnitOfWorkRetry
{
    public static Task<TResult> ExecuteAsync<TResult>(
        IExecutionStrategy strategy,
        Func<CancellationToken, Task<IDbTransaction>> beginTransaction,
        Action prepareRetry,
        Func<CancellationToken, Task<TResult>> operation,
        Func<TResult, bool> shouldCommit,
        CancellationToken cancellationToken)
    {
        var isRetry = false;
        return strategy.ExecuteAsync(
            async token =>
            {
                // Only a retry starts clean: the first attempt saves what the caller changed before the call.
                if (isRetry)
                {
                    prepareRetry();
                }

                isRetry = true;
                var transaction = await beginTransaction(token).ConfigureAwait(false);
                await using var _ = transaction.ConfigureAwait(false);
                var result = await operation(token).ConfigureAwait(false);
                if (shouldCommit(result))
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                else
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                }

                return result;
            },
            cancellationToken);
    }

    /// <summary>
    /// Readies <paramref name="contexts"/> for a retry of the operation: clears their change trackers, or throws when
    /// they held unsaved changes before the call, because clearing would drop them and the operation cannot repeat
    /// them.
    /// </summary>
    /// <param name="contexts">The contexts the operation runs on.</param>
    /// <param name="hadUnsavedChanges">Whether a change tracker held unsaved changes before the first attempt.</param>
    /// <exception cref="InvalidOperationException"><paramref name="hadUnsavedChanges"/> is <see langword="true"/>.</exception>
    public static void Prepare(IEnumerable<DbContext> contexts, bool hadUnsavedChanges)
    {
        if (hadUnsavedChanges)
        {
            throw new InvalidOperationException(
                "A transient fault interrupted ExecuteInTransactionAsync, and the operation cannot be retried: the change tracker held unsaved changes before the call, and a retry runs the operation from a clean change tracker. Make the changes inside the operation, so that a retry can repeat them.");
        }

        foreach (var context in contexts)
        {
            context.ChangeTracker.Clear();
        }
    }
}
