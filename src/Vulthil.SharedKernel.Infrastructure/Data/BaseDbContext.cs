using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// Abstract base <see cref="DbContext"/> that implements <see cref="IUnitOfWork"/> and <see cref="ISaveOutboxMessages"/> for outbox integration.
/// </summary>
/// <param name="options">The options for configuring the context.</param>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options), IUnitOfWork, ISaveOutboxMessages
{
    /// <inheritdoc />
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    public bool IsInTransaction => Database.CurrentTransaction is not null;

    /// <summary>
    /// Gets the assembly containing EF Core entity type configurations to apply during model creation.
    /// Return <see langword="null"/> to skip automatic configuration scanning.
    /// </summary>
    protected abstract Assembly? ConfigurationAssembly { get; }
    /// <summary>
    /// Gets an optional type filter predicate applied when scanning for entity type configurations.
    /// When <see langword="null"/>, all configurations in <see cref="ConfigurationAssembly"/> are applied.
    /// </summary>
    protected virtual Func<Type, bool>? ConfigurationTypeConstraints { get; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (ConfigurationAssembly is not null)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly, ConfigurationTypeConstraints);
        }
    }

    /// <inheritdoc />
    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(operation, static _ => true, cancellationToken);

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, Func<TResult, bool> shouldCommit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(shouldCommit);

        if (Database.CurrentTransaction is not null)
        {
            // Already inside a transaction (e.g. an outer filter opened it) — join it; the outer owns the commit.
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        var hadUnsavedChanges = ChangeTracker.HasChanges();
        var isRetry = false;
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token =>
            {
                // Only a retry starts clean: the first attempt saves what the caller changed before the call.
                if (isRetry)
                {
                    PrepareRetry(hadUnsavedChanges);
                }

                isRetry = true;
                var transaction = await Database.BeginTransactionAsync(token).ConfigureAwait(false);
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
    /// Readies the context for a retry of the operation: clears the change tracker so the retry starts from a clean
    /// state, or throws when the context held unsaved changes before the call, because clearing would drop them and the
    /// operation cannot repeat them.
    /// </summary>
    /// <param name="hadUnsavedChanges">Whether the change tracker held unsaved changes before the first attempt.</param>
    /// <exception cref="InvalidOperationException"><paramref name="hadUnsavedChanges"/> is <see langword="true"/>.</exception>
    private void PrepareRetry(bool hadUnsavedChanges)
    {
        if (hadUnsavedChanges)
        {
            throw new InvalidOperationException(
                "A transient fault interrupted ExecuteInTransactionAsync, and the operation cannot be retried: the context held unsaved changes before the call, and a retry runs the operation from a clean change tracker. Make the changes inside the operation, so that a retry can repeat them.");
        }

        ChangeTracker.Clear();
    }
}
