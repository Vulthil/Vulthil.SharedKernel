using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

/// <summary>
/// Outbox store for relational database providers. Records processed and failed messages with set-based
/// <c>ExecuteUpdate</c> calls instead of materializing rows, and composes the row-locking fetch statement that
/// provider packages (e.g. Npgsql, MySQL) opt into by overriding
/// <see cref="EntityFrameworkOutboxStore{TContext}.FetchMessagesAsync"/> with a call to
/// <see cref="FetchMessagesWithRowLockAsync"/> and their dialect's lock clause.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class RelationalOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : EntityFrameworkOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    private OutboxSqlIdentifiers? _identifiers;

    /// <summary>
    /// Opens the transaction for the relay batch, requiring <typeparamref name="TContext"/> to support one.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The transaction to commit on success.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TContext"/> does not implement <see cref="IUnitOfWork"/>, so no transaction could be opened.
    /// </exception>
    protected override async Task<IDbTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await base.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        return transaction ?? throw new InvalidOperationException(
            $"RelationalOutboxStore could not open a transaction because '{typeof(TContext).Name}' does not implement " +
            "IUnitOfWork. Without a transaction, provider row-locking (e.g. FOR UPDATE SKIP LOCKED) releases immediately " +
            "after the fetch statement, so concurrent relay instances can double-dispatch the same messages. Derive " +
            $"'{typeof(TContext).Name}' from BaseDbContext or implement IUnitOfWork so a transaction can be opened.");
    }

    /// <summary>
    /// Fetches a batch of unprocessed messages with a raw <c>SELECT</c> that ends in <paramref name="rowLockClause"/>
    /// (e.g. <c>FOR UPDATE SKIP LOCKED</c>), so concurrent relay instances claim disjoint rows. The statement is
    /// composed from the model's mapped table and column names, so custom identifiers (a naming convention,
    /// <c>ToTable</c>, or <c>HasColumnName</c>) are supported, and it carries an explicit outer <c>ORDER BY</c> so
    /// dispatch order is deterministic. Provider stores call this from their
    /// <see cref="EntityFrameworkOutboxStore{TContext}.FetchMessagesAsync"/> override.
    /// </summary>
    /// <param name="rowLockClause">
    /// The provider's row-locking clause, appended after the <c>LIMIT</c>; an empty string fetches without locking.
    /// </param>
    /// <param name="batchSize">The maximum number of messages to fetch.</param>
    /// <param name="maxRetries">Messages at or above this retry count are excluded.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The fetched message data, oldest first.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TContext"/> does not map the outbox entity, or one of the relay's columns, to a table.
    /// </exception>
    protected Task<List<OutboxMessageData>> FetchMessagesWithRowLockAsync(string rowLockClause, int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rowLockClause);
        _identifiers ??= OutboxSqlIdentifiers.Resolve(DbContext);

        var fetchSqlFormat = $$"""
            SELECT * FROM {{_identifiers.Table}}
            WHERE {{_identifiers.ProcessedOnUtc}} IS NULL AND {{_identifiers.FailedOnUtc}} IS NULL AND {{_identifiers.RetryCount}} < {0}
            ORDER BY {{_identifiers.OccurredOnUtc}}, {{_identifiers.Id}}
            LIMIT {1}
            {{rowLockClause}}
            """;

        return OutboxMessages
            .FromSql(FormattableStringFactory.Create(fetchSqlFormat, maxRetries, batchSize))
            .OrderBy(x => x.OccurredOnUtc)
            .ThenBy(x => x.Id)
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task UpdateMessagesAsync(IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken)
    {
        if (successIds.Count > 0)
        {
            await OutboxMessages
                .Where(x => successIds.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(o => o.ProcessedOnUtc, processedOnUtc),
                    cancellationToken).ConfigureAwait(false);
        }

        foreach (var failure in failures)
        {
            await OutboxMessages
                .Where(x => x.Id == failure.Id)
                .ExecuteUpdateAsync(
                    setter => setter
                        .SetProperty(o => o.RetryCount, o => o.RetryCount + 1)
                        .SetProperty(o => o.Error, failure.Error),
                    cancellationToken).ConfigureAwait(false);
        }

        if (failures.Count > 0)
        {
            var failedIds = failures.Select(f => f.Id).ToList();

            var deadLettered = await OutboxMessages
                .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                .Select(x => new { x.Id, x.RetryCount, x.Error })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (deadLettered.Count > 0)
            {
                await OutboxMessages
                    .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(o => o.FailedOnUtc, processedOnUtc),
                        cancellationToken).ConfigureAwait(false);

                foreach (var item in deadLettered)
                {
                    Logger.LogError("Outbox message {OutboxMessageId} dead-lettered after {RetryCount} failed attempts: {OutboxError}", item.Id, item.RetryCount, item.Error);
                }
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Selects the keys of the oldest eligible rows first and deletes by key, so a single call never removes more
    /// than <paramref name="batchSize"/> rows — a large backlog is drained in bounded, short-lived deletes instead
    /// of one long-locking statement.
    /// </remarks>
    public override async Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await OutboxMessages
            .Where(o => o.ProcessedOnUtc != null && o.ProcessedOnUtc < olderThanUtc
                || o.FailedOnUtc != null && o.FailedOnUtc < olderThanUtc)
            .OrderBy(o => o.OccurredOnUtc)
            .Take(batchSize)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await OutboxMessages
            .Where(o => ids.Contains(o.Id))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record OutboxSqlIdentifiers(string Table, string Id, string OccurredOnUtc, string ProcessedOnUtc, string FailedOnUtc, string RetryCount)
    {
        public static OutboxSqlIdentifiers Resolve(TContext dbContext)
        {
            var entityType = dbContext.Model.FindEntityType(typeof(OutboxMessage))
                ?? throw new InvalidOperationException(
                    $"'{typeof(TContext).Name}' does not map the OutboxMessage entity; apply the outbox mapping (ApplyOutbox, or your provider's Apply*Outbox extension) in OnModelCreating.");
            var tableName = entityType.GetTableName()
                ?? throw new InvalidOperationException(
                    $"The OutboxMessage entity in '{typeof(TContext).Name}' is not mapped to a table, so the relay fetch SQL cannot be composed.");
            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            var sqlGenerationHelper = dbContext.GetService<ISqlGenerationHelper>();

            return new OutboxSqlIdentifiers(
                sqlGenerationHelper.DelimitIdentifier(tableName, entityType.GetSchema()),
                Column(nameof(OutboxMessage.Id)),
                Column(nameof(OutboxMessage.OccurredOnUtc)),
                Column(nameof(OutboxMessage.ProcessedOnUtc)),
                Column(nameof(OutboxMessage.FailedOnUtc)),
                Column(nameof(OutboxMessage.RetryCount)));

            string Column(string propertyName)
            {
                var columnName = entityType.FindProperty(propertyName)?.GetColumnName(storeObject)
                    ?? throw new InvalidOperationException(
                        $"The OutboxMessage property '{propertyName}' is not mapped to a column of '{tableName}', so the relay fetch SQL cannot be composed.");
                return sqlGenerationHelper.DelimitIdentifier(columnName);
            }
        }
    }
}
