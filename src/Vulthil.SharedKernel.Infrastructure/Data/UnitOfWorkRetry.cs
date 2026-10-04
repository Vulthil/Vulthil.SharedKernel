using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// The retry rule every unit of work in this package shares: a retry runs the operation from a clean change tracker,
/// so it cannot repeat changes made before the call and throws instead of dropping them.
/// </summary>
internal static class UnitOfWorkRetry
{
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
