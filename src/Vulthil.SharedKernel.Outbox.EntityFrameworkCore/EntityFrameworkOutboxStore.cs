using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of <see cref="IOutboxStore"/>. It owns the relay batch unit — runs inside the
/// context's execution strategy, opens the transaction, fetches a batch, dispatches each message, records the
/// outcome, and commits — and exposes the capture surface used by bus-publish filters. Provider packages derive from
/// this type to add row-level locking (<see cref="FetchMessagesAsync"/>), set-based updates
/// (<see cref="UpdateMessagesAsync"/>), or a no-op transaction (<see cref="BeginTransactionAsync"/>).
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class EntityFrameworkOutboxStore<TContext> : IOutboxStore, IOutboxRetentionStore
    where TContext : DbContext, ISaveOutboxMessages
{
    private readonly TimeProvider _timeProvider;
    private readonly OutboxProcessingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkOutboxStore{TContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The application's persistence context.</param>
    /// <param name="timeProvider">The time provider used to stamp processed messages.</param>
    /// <param name="options">The outbox processing options (batch size, retry limit, parallelism).</param>
    public EntityFrameworkOutboxStore(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);

        DbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        Logger = dbContext.GetService<ILoggerFactory>().CreateLogger(GetType());
    }

    /// <summary>Gets the application's persistence context.</summary>
    protected TContext DbContext { get; }

    /// <summary>Gets a logger resolved from the persistence context's logging integration.</summary>
    protected ILogger Logger { get; }

    /// <summary>Gets the tracked outbox message set.</summary>
    protected DbSet<OutboxMessage> OutboxMessages => DbContext.OutboxMessages;

    /// <inheritdoc />
    public bool IsInTransaction => DbContext.IsInTransaction;

    /// <inheritdoc />
    public void AddOutboxMessage(OutboxMessage message) => DbContext.OutboxMessages.Add(message);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => DbContext.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        var strategy = DbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                DbContext.ChangeTracker.Clear();
                return await ProcessBatchCoreAsync(dispatch, token);
            },
            cancellationToken);
    }

    private async Task<int> ProcessBatchCoreAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken)
    {
        var transaction = await BeginTransactionAsync(cancellationToken);

        try
        {
            var messages = await FetchMessagesAsync(_options.BatchSize, _options.MaxRetries, cancellationToken);

            if (messages.Count == 0)
            {
                return 0;
            }

            var successIds = new List<Guid>();
            var failures = new List<OutboxMessageFailure>();

            if (_options.EnableParallelPublishing)
            {
                using var throttle = new SemaphoreSlim(_options.MaxDegreeOfParallelism);
                var outcomes = await Task.WhenAll(messages.Select(message => DispatchThrottledAsync(message, dispatch, throttle, cancellationToken)));
                foreach (var (id, error) in outcomes)
                {
                    Record(successIds, failures, id, error);
                }
            }
            else
            {
                foreach (var message in messages)
                {
                    var error = await dispatch(message, cancellationToken);
                    Record(successIds, failures, message.Id, error);
                }
            }

            await UpdateMessagesAsync(successIds, failures, _options.MaxRetries, _timeProvider.GetUtcNow(), cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return messages.Count;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static void Record(List<Guid> successIds, List<OutboxMessageFailure> failures, Guid id, string? error)
    {
        if (error is null)
        {
            successIds.Add(id);
        }
        else
        {
            failures.Add(new OutboxMessageFailure(id, error));
        }
    }

    private static async Task<(Guid Id, string? Error)> DispatchThrottledAsync(
        OutboxMessageData message,
        Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch,
        SemaphoreSlim throttle,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken);
        try
        {
            return (message.Id, await dispatch(message, cancellationToken));
        }
        finally
        {
            throttle.Release();
        }
    }

    /// <summary>
    /// Begins the transaction for the relay batch. The default enlists the context's <see cref="IUnitOfWork"/>;
    /// providers without ambient transactions (e.g. Cosmos) override this.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The transaction to commit on success, or <see langword="null"/> when none is used.</returns>
    protected virtual async Task<IDbTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (DbContext is IUnitOfWork unitOfWork)
        {
            return await unitOfWork.BeginTransactionAsync(cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Fetches a batch of unprocessed messages. Providers override this to add row-level locking
    /// (e.g. <c>FOR UPDATE SKIP LOCKED</c>).
    /// </summary>
    /// <param name="batchSize">The maximum number of messages to fetch.</param>
    /// <param name="maxRetries">Messages at or above this retry count are excluded.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The fetched message data.</returns>
    protected virtual Task<List<OutboxMessageData>> FetchMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken) =>
        OutboxMessages
            .Where(o => o.ProcessedOnUtc == null && o.FailedOnUtc == null && o.RetryCount < maxRetries)
            .OrderBy(o => o.OccurredOnUtc)
            .ThenBy(o => o.Id)
            .Take(batchSize)
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Records processed and failed messages, dead-lettering any that reach <paramref name="maxRetries"/>. The default
    /// materializes and updates the rows; relational providers override this with set-based <c>ExecuteUpdate</c> calls.
    /// </summary>
    /// <param name="successIds">Identifiers of successfully delivered messages.</param>
    /// <param name="failures">Failures to record with a retry increment.</param>
    /// <param name="maxRetries">Failed messages reaching this retry count are dead-lettered (their <c>FailedOnUtc</c> is set).</param>
    /// <param name="processedOnUtc">The timestamp to record for processed or dead-lettered messages.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    protected virtual async Task UpdateMessagesAsync(IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken)
    {
        if (successIds.Count > 0)
        {
            var messages = await OutboxMessages.Where(x => successIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
            foreach (var item in messages)
            {
                item.ProcessedOnUtc = processedOnUtc;
            }

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var failure in failures)
        {
            var messages = await OutboxMessages.Where(x => x.Id == failure.Id).ToArrayAsync(cancellationToken);
            foreach (var item in messages)
            {
                item.RetryCount++;
                item.Error = failure.Error;
            }

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        if (failures.Count > 0)
        {
            var failedIds = failures.Select(f => f.Id).ToList();
            var deadLettered = await OutboxMessages.Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries).ToArrayAsync(cancellationToken);
            foreach (var item in deadLettered)
            {
                item.FailedOnUtc = processedOnUtc;
                Logger.LogError("Outbox message {OutboxMessageId} dead-lettered after {RetryCount} failed attempts: {OutboxError}", item.Id, item.RetryCount, item.Error);
            }

            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
    {
        var rows = await OutboxMessages
            .Where(o => o.ProcessedOnUtc != null && o.ProcessedOnUtc < olderThanUtc
                || o.FailedOnUtc != null && o.FailedOnUtc < olderThanUtc)
            .OrderBy(o => o.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        OutboxMessages.RemoveRange(rows);
        await DbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }
}
