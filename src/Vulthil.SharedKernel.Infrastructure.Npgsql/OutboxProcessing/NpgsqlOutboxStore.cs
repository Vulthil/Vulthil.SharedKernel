using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// PostgreSQL-optimized outbox store that fetches messages with <c>SELECT ... FOR UPDATE SKIP LOCKED</c> so multiple
/// processors can drain distinct rows concurrently. The statement is composed by
/// <see cref="RelationalOutboxStore{TContext}.FetchMessagesWithRowLockAsync"/> from the model's mapped table and
/// column names, so custom identifiers (a naming convention, <c>ToTable</c>, or <c>HasColumnName</c>) are supported.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class NpgsqlOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : RelationalOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    private const string RowLockClause = "FOR UPDATE SKIP LOCKED";

    /// <inheritdoc />
    protected override Task<List<OutboxMessageData>> FetchMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken) =>
        FetchMessagesWithRowLockAsync(RowLockClause, batchSize, maxRetries, cancellationToken);
}
