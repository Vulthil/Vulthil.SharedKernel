namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// An <see cref="IOutboxStore"/> whose relay unit claims from a fixed set of messages and keeps the recorded outcomes in
/// memory, so engine tests drive the real <see cref="OutboxRelayCycle"/> without a database.
/// </summary>
internal sealed class InMemoryRelayStore : IOutboxStore
{
    private readonly TaskCompletionSource _secondRunStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<(int BatchSize, int MaxRetries)> _claims = [];
    private int _runCount;

    public InMemoryRelayStore(int messageCount)
        : this(CreateMessages(messageCount))
    {
    }

    public InMemoryRelayStore(IReadOnlyList<OutboxMessageData> messages) => Messages = messages;

    public IReadOnlyList<OutboxMessageData> Messages { get; }

    public IReadOnlyList<(int BatchSize, int MaxRetries)> Claims => _claims;

    public int RecordCount { get; private set; }

    public IReadOnlyList<Guid> RelayedIds { get; private set; } = [];

    public IReadOnlyList<OutboxMessageFailure> Failures { get; private set; } = [];

    public int RecordedMaxRetries { get; private set; }

    public int StepCount { get; private set; }

    public int FailedStepCount { get; private set; }

    /// <summary>Gets a value indicating whether the relay unit dead-letters every failure it records.</summary>
    public bool DeadLettersFailures { get; init; }

    /// <summary>
    /// Gets a value indicating whether each relay unit runs twice, like a store whose execution strategy runs the unit
    /// again after its commit failed.
    /// </summary>
    public bool RunsEachUnitTwice { get; init; }

    public Task SecondRunStarted => _secondRunStarted.Task;

    public bool IsInTransaction => false;

    public void AddOutboxMessage(OutboxMessage message)
    {
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task<TResult> RunRelayUnitAsync<TResult>(Func<IOutboxRelayUnit, CancellationToken, Task<TResult>> unit, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _runCount) == 2)
        {
            _secondRunStarted.TrySetResult();
        }

        if (RunsEachUnitTwice)
        {
            await unit(new RelayUnit(this), cancellationToken);
        }

        return await unit(new RelayUnit(this), cancellationToken);
    }

    public static List<OutboxMessageData> CreateMessages(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new OutboxMessageData(Guid.NewGuid(), "Some.Event", "{}", null, null, OutboxDestination.DomainEvent, null))
            .ToList();

    private sealed class RelayUnit(InMemoryRelayStore store) : IOutboxRelayUnit
    {
        public Task<IReadOnlyList<OutboxMessageData>> ClaimAsync(int batchSize, int maxRetries, CancellationToken cancellationToken)
        {
            store._claims.Add((batchSize, maxRetries));
            return Task.FromResult<IReadOnlyList<OutboxMessageData>>(store.Messages.Take(batchSize).ToList());
        }

        public async Task RunStepAsync(Func<CancellationToken, Task> step, CancellationToken cancellationToken)
        {
            store.StepCount++;
            try
            {
                await step(cancellationToken);
            }
            catch
            {
                store.FailedStepCount++;
                throw;
            }
        }

        public Task<IReadOnlyList<Guid>> RecordAsync(IReadOnlyList<Guid> relayedIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, CancellationToken cancellationToken)
        {
            store.RecordCount++;
            store.RelayedIds = relayedIds;
            store.Failures = failures;
            store.RecordedMaxRetries = maxRetries;
            IReadOnlyList<Guid> deadLetteredIds = store.DeadLettersFailures ? failures.Select(failure => failure.Id).ToList() : [];
            return Task.FromResult(deadLetteredIds);
        }
    }
}
