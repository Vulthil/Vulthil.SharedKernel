using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// PostgreSQL-optimized outbox store that fetches messages with <c>SELECT ... FOR UPDATE SKIP LOCKED</c> so multiple
/// processors can drain distinct rows concurrently.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class NpgsqlOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : RelationalOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    /// <inheritdoc />
    protected override Task<List<OutboxMessageData>> FetchMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken) =>
        OutboxMessages.FromSqlInterpolated($@"
            SELECT ""Id"", ""Type"", ""Content"", ""TraceParent"", ""TraceState"", ""Destination"", ""Metadata""
            FROM ""OutboxMessages""
            WHERE ""ProcessedOnUtc"" IS NULL AND ""RetryCount"" < {maxRetries}
            ORDER BY ""OccurredOnUtc""
            FOR UPDATE SKIP LOCKED
            LIMIT {batchSize}")
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);
}
