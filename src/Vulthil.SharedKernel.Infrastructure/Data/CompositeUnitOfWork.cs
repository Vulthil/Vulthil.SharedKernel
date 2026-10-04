using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// The host's <see cref="IUnitOfWork"/> when it registers several contexts: saves and transactions span every context.
/// The contexts commit one by one in commit order — registration order, with the outbox context last — so a failed
/// commit can leave a saved change without its message, but never a message for a change that was not saved.
/// </summary>
/// <remarks>
/// A commit is atomic per context only. When a commit fails after another context committed, the committed changes
/// stay saved, the rest of the unit is rolled back, and the unit throws instead of retrying, because a retry would
/// repeat the committed changes. Retries follow the execution strategy of the first registered context that the unit
/// opens a transaction for. A context whose provider has no transactions (for example Cosmos DB) stays out of the
/// transaction.
/// </remarks>
internal sealed class CompositeUnitOfWork : IUnitOfWork
{
    private readonly IReadOnlyList<DbContext> _contexts;
    private readonly IReadOnlyList<DbContext> _commitOrder;

    /// <summary>
    /// Initializes a unit of work over <paramref name="contexts"/>.
    /// </summary>
    /// <param name="contexts">The registered contexts, in registration order.</param>
    /// <param name="outboxContext">The outbox-enabled context, which commits last; <see langword="null"/> when there is none.</param>
    public CompositeUnitOfWork(IReadOnlyList<DbContext> contexts, DbContext? outboxContext)
    {
        _contexts = contexts;
        _commitOrder = outboxContext is null
            ? contexts
            : [.. contexts.Where(context => !ReferenceEquals(context, outboxContext)), outboxContext];
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var written = 0;
        foreach (var context in _commitOrder)
        {
            written += await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return written;
    }

    /// <inheritdoc />
    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        await BeginTransactionsAsync(_commitOrder, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(operation, static _ => true, cancellationToken);

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, Func<TResult, bool> shouldCommit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(shouldCommit);

        // A context that already has a transaction joins it, and that transaction's owner commits it; the unit opens
        // and commits transactions only for the other contexts.
        var unitContexts = _commitOrder.Where(static context => context.Database.CurrentTransaction is null).ToList();
        if (unitContexts.Count == 0)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        var joinsOpenTransactions = unitContexts.Count < _commitOrder.Count;
        var hadUnsavedChanges = _contexts.Any(static context => context.ChangeTracker.HasChanges());
        var isRetry = false;
        var strategy = _contexts.First(unitContexts.Contains).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token =>
            {
                if (isRetry)
                {
                    PrepareRetry(joinsOpenTransactions, hadUnsavedChanges);
                }

                isRetry = true;
                var transaction = await BeginTransactionsAsync(unitContexts, token).ConfigureAwait(false);
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
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Readies the contexts for a retry, or throws when part of the unit runs in a transaction that the unit did not
    /// open: the retry would repeat that work inside it.
    /// </summary>
    private void PrepareRetry(bool joinsOpenTransactions, bool hadUnsavedChanges)
    {
        if (joinsOpenTransactions)
        {
            throw new InvalidOperationException(
                "A transient fault interrupted ExecuteInTransactionAsync, and the operation cannot be retried: part of the unit runs in a transaction that the unit did not open, and a retry would repeat that work inside it.");
        }

        UnitOfWorkRetry.Prepare(_contexts, hadUnsavedChanges);
    }

    private static async Task<CompositeTransaction> BeginTransactionsAsync(IEnumerable<DbContext> contexts, CancellationToken cancellationToken)
    {
        var transaction = new CompositeTransaction();
        try
        {
            foreach (var context in contexts)
            {
                if (await TryBeginTransactionAsync(context, cancellationToken).ConfigureAwait(false) is { } contextTransaction)
                {
                    transaction.Add(context, contextTransaction);
                }
            }

            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<IDbContextTransaction?> TryBeginTransactionAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            // The provider has no transactions (for example Cosmos DB), so the context stays out of the transaction
            // and its saves are not rolled back with the others.
            return null;
        }
    }

    /// <summary>
    /// The transactions of one unit, committed one by one in commit order.
    /// </summary>
    private sealed class CompositeTransaction : IDbTransaction
    {
        private readonly List<(DbContext Context, IDbContextTransaction Transaction)> _transactions = [];

        public void Add(DbContext context, IDbContextTransaction transaction) => _transactions.Add((context, transaction));

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < _transactions.Count; index++)
            {
                try
                {
                    // Once a context has committed, the later commits ignore cancellation: stopping halfway would turn
                    // a cancelled unit into a partial commit.
                    await _transactions[index].Transaction.CommitAsync(index == 0 ? cancellationToken : CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (index > 0)
                {
                    throw PartialCommit(index, exception);
                }
            }
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            foreach (var (_, transaction) in _transactions)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var (_, transaction) in _transactions)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        private InvalidOperationException PartialCommit(int failedIndex, Exception exception)
        {
            var committed = string.Join(", ", _transactions.Take(failedIndex).Select(static entry => $"'{entry.Context.GetType().Name}'"));
            var failed = _transactions[failedIndex].Context.GetType().Name;
            return new InvalidOperationException(
                $"The unit of work committed {committed} but could not commit '{failed}', so the rest of the unit is rolled back. " +
                "The committed changes stay saved. The operation is not retried, because a retry would repeat them.",
                exception);
        }
    }
}
