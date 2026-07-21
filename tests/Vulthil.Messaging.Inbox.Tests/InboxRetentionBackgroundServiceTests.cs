using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class InboxRetentionBackgroundServiceTests : BaseUnitTestCase
{
    private readonly Lazy<InboxRetentionBackgroundService> _lazyTarget;

    private InboxRetentionBackgroundService Target => _lazyTarget.Value;

    public InboxRetentionBackgroundServiceTests()
    {
        _lazyTarget = new(CreateInstance<InboxRetentionBackgroundService>);
        Use(TimeProvider.System);
    }

    [Fact]
    public async Task StoreWithoutRetentionSupportLogsExactlyOneWarningAcrossMultipleSweeps()
    {
        // Arrange
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions
        {
            Retention = { SweepInterval = TimeSpan.FromMilliseconds(5) }
        }));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var logger = new WarningCountingLogger();
        Use<ILogger<InboxRetentionBackgroundService>>(logger);

        // Act
        await Target.StartAsync(CancellationToken);
        await Task.WhenAny(logger.FirstWarning, Task.Delay(TimeSpan.FromSeconds(5), CancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        logger.WarningCount.ShouldBe(1);
    }

    [Fact]
    public async Task AForeignCancellationDuringASweepDoesNotEndTheExecuteTask()
    {
        // Arrange
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions()));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new TimeoutThrowingRetentionStore();
        Use<IIdempotencyStore>(store);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.FirstSweepAttempted;
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task TheSweepPassesACutoffComputedFromTheConfiguredRetentionPeriod()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var retentionPeriod = TimeSpan.FromDays(3);
        Use<TimeProvider>(new FixedTimeProvider(now));
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions
        {
            Retention = { SweepInterval = TimeSpan.FromSeconds(60), RetentionPeriod = retentionPeriod }
        }));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new RecordingRetentionStore([0]);
        Use<IIdempotencyStore>(store);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.LastCallCompleted;
        await Target.StopAsync(CancellationToken);

        // Assert
        store.ObservedCutoffs.ShouldHaveSingleItem().ShouldBe(now - retentionPeriod);
    }

    [Fact]
    public async Task TheSweepKeepsDeletingBatchesUntilFewerThanBatchSizeRowsRemain()
    {
        // Arrange
        const int batchSize = 5;
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions
        {
            Retention = { SweepInterval = TimeSpan.FromSeconds(60), BatchSize = batchSize }
        }));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new RecordingRetentionStore([batchSize, batchSize, 2]);
        Use<IIdempotencyStore>(store);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.LastCallCompleted;
        await Target.StopAsync(CancellationToken);

        // Assert
        store.CallCount.ShouldBe(3);
        store.ObservedBatchSizes.ShouldAllBe(size => size == batchSize);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Records every <see cref="DeleteProcessedAsync"/> call's cutoff/batch-size and replays a fixed result sequence.</summary>
    private sealed class RecordingRetentionStore(IReadOnlyList<int> resultsInOrder) : IIdempotencyStore, IInboxRetentionStore
    {
        private readonly TaskCompletionSource _lastCallCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DateTimeOffset> _observedCutoffs = [];
        private readonly List<int> _observedBatchSizes = [];
        private int _callCount;

        public Task LastCallCompleted => _lastCallCompleted.Task;

        public IReadOnlyList<DateTimeOffset> ObservedCutoffs => _observedCutoffs;

        public IReadOnlyList<int> ObservedBatchSizes => _observedBatchSizes;

        public int CallCount => _callCount;

        public Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            _observedCutoffs.Add(olderThanUtc);
            _observedBatchSizes.Add(batchSize);

            var result = index < resultsInOrder.Count ? resultsInOrder[index] : resultsInOrder[^1];
            if (index == resultsInOrder.Count - 1)
            {
                _lastCallCompleted.TrySetResult();
            }

            return Task.FromResult(result);
        }
    }

    private sealed class TimeoutThrowingRetentionStore : IIdempotencyStore, IInboxRetentionStore
    {
        private readonly TaskCompletionSource _firstSweepAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstSweepAttempted => _firstSweepAttempted.Task;

        public Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            _firstSweepAttempted.TrySetResult();
            throw new OperationCanceledException();
        }
    }

    private sealed class WarningCountingLogger : ILogger<InboxRetentionBackgroundService>
    {
        private readonly TaskCompletionSource _firstWarning = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WarningCount { get; private set; }

        public Task FirstWarning => _firstWarning.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning)
            {
                return;
            }

            WarningCount++;
            _firstWarning.TrySetResult();
        }
    }

    private sealed class AutoMockerServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new PassthroughServiceScope(serviceProvider);

        private sealed class PassthroughServiceScope(IServiceProvider serviceProvider) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = serviceProvider;

            public void Dispose()
            {
            }
        }
    }
}
