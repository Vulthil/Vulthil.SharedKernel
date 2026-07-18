using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;

/// <summary>
/// MySQL-optimized outbox store that fetches messages with <c>SELECT ... FOR UPDATE SKIP LOCKED</c> so multiple
/// processors can drain distinct rows concurrently. The fetch SQL is composed from the model's mapped table and
/// column names, so custom identifiers (a naming convention, <c>ToTable</c>, or <c>HasColumnName</c>) are supported,
/// and the composed query carries an explicit outer <c>ORDER BY</c> so dispatch order is deterministic.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class MySqlOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : RelationalOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    private string? _fetchSqlFormat;

    /// <inheritdoc />
    protected override Task<List<OutboxMessageData>> FetchMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        _fetchSqlFormat ??= BuildFetchSqlFormat();

        return OutboxMessages
            .FromSql(FormattableStringFactory.Create(_fetchSqlFormat, maxRetries, batchSize))
            .OrderBy(x => x.OccurredOnUtc)
            .ThenBy(x => x.Id)
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);
    }

    private string BuildFetchSqlFormat()
    {
        var identifiers = OutboxSqlIdentifiers.Resolve(DbContext);

        return $$"""
            SELECT * FROM {{identifiers.Table}}
            WHERE {{identifiers.ProcessedOnUtc}} IS NULL AND {{identifiers.FailedOnUtc}} IS NULL AND {{identifiers.RetryCount}} < {0}
            ORDER BY {{identifiers.OccurredOnUtc}}, {{identifiers.Id}}
            LIMIT {1} FOR UPDATE SKIP LOCKED
            """;
    }

    private sealed record OutboxSqlIdentifiers(string Table, string Id, string OccurredOnUtc, string ProcessedOnUtc, string FailedOnUtc, string RetryCount)
    {
        public static OutboxSqlIdentifiers Resolve(TContext dbContext)
        {
            var entityType = dbContext.Model.FindEntityType(typeof(OutboxMessage))
                ?? throw new InvalidOperationException(
                    $"'{typeof(TContext).Name}' does not map the OutboxMessage entity; apply the outbox mapping (e.g. ApplyMySqlOutbox) in OnModelCreating.");
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
