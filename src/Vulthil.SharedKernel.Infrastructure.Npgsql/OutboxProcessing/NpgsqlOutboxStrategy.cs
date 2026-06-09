using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// PostgreSQL-optimized outbox strategy that uses SELECT ... FOR UPDATE SKIP LOCKED to fetch messages concurrently.
/// </summary>
public class NpgsqlOutboxStrategy : RelationalOutboxStrategy
{
    /// <inheritdoc />
    public override async Task<List<OutboxMessageData>> FetchMessagesAsync(DbSet<OutboxMessage> outboxMessages, int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        // Use SELECT ... FOR UPDATE SKIP LOCKED to allow multiple processors to fetch distinct rows concurrently.
        var query = outboxMessages.FromSqlInterpolated($@"
            SELECT ""Id"", ""Type"", ""Content"", ""TraceParent"", ""TraceState"", ""Destination"", ""Metadata""
            FROM ""OutboxMessages""
            WHERE ""ProcessedOnUtc"" IS NULL AND ""RetryCount"" < {maxRetries}
            ORDER BY ""OccurredOnUtc""
            FOR UPDATE SKIP LOCKED
            LIMIT {batchSize}");

        return await query
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);
    }
}
