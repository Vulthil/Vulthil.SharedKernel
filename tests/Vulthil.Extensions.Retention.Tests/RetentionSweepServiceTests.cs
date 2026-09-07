using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Retention.Tests;

public sealed class RetentionSweepServiceTests : BaseUnitTestCase
{
    private const string SweepName = "Probe";
    private const int BatchSize = 5;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(3);
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly TimerAwareFakeTimeProvider _timeProvider = new();
    private readonly RecordingStore _store = new();
    private readonly CapturingLogger _logger = new();
    private readonly Lazy<RetentionSweepService<IProbeStore>> _lazyTarget;

    private RetentionSweepService<IProbeStore> Target => _lazyTarget.Value;

    public RetentionSweepServiceTests()
    {
        _timeProvider.SetUtcNow(Now);
        Use(SweepName);
        Use(new RetentionSweepSettings(RetentionPeriod, SweepInterval, BatchSize));
        Use<Func<IProbeStore, RetentionSweepDeleter?>>(static store => store.Deleter);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<TimeProvider>(_timeProvider);
        Use<ILogger<RetentionSweepService<IProbeStore>>>(_logger);
        Use<IProbeStore>(_store);
        _lazyTarget = new(CreateInstance<RetentionSweepService<IProbeStore>>);
    }

    [Fact]
    public async Task TheFirstSweepRunsOnStartWithACutoffComputedFromTheRetentionPeriod()
    {
        // Arrange
        _store.Results = [0];

        // Act
        await Target.StartAsync(CancellationToken);
        await _store.LastCallCompleted.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        _store.ObservedCutoffs.ShouldHaveSingleItem().ShouldBe(Now - RetentionPeriod);
        _store.ObservedBatchSizes.ShouldHaveSingleItem().ShouldBe(BatchSize);
    }

    [Fact]
    public async Task TheSweepKeepsDeletingBatchesUntilFewerThanBatchSizeEntriesRemain()
    {
        // Arrange
        _store.Results = [BatchSize, BatchSize, 2];

        // Act
        await Target.StartAsync(CancellationToken);
        await _store.LastCallCompleted.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        _store.CallCount.ShouldBe(3);
        _store.ObservedBatchSizes.ShouldAllBe(size => size == BatchSize);
        _logger.Messages(LogLevel.Information).ShouldHaveSingleItem().ShouldContain($"{SweepName} retention deleted 12");
    }

    [Fact]
    public async Task TheSweepRunsAgainAfterTheSweepInterval()
    {
        // Arrange
        _store.Results = [0, 0];

        // Act
        await Target.StartAsync(CancellationToken);
        await _timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(SweepInterval);
        await _store.LastCallCompleted.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        _store.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task AStoreWithoutRetentionSupportIsWarnedAboutExactlyOnceAcrossSweeps()
    {
        // Arrange
        _store.SupportsRetention = false;

        // Act
        await Target.StartAsync(CancellationToken);
        await _store.AdapterCalled(1).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await _timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(SweepInterval);
        await _store.AdapterCalled(2).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        _store.CallCount.ShouldBe(0);
        _logger.Messages(LogLevel.Warning).ShouldHaveSingleItem().ShouldContain($"{SweepName} retention is enabled");
    }

    [Fact]
    public async Task AForeignCancellationDuringASweepIsLoggedAndDoesNotEndTheExecuteTask()
    {
        // Arrange
        _store.ExceptionToThrow = new OperationCanceledException();

        // Act
        await Target.StartAsync(CancellationToken);
        await _store.AdapterCalled(1).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await _logger.Logged(LogLevel.Error).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        var executeTaskAfterTheFailure = Target.ExecuteTask!.Status;
        await Target.StopAsync(CancellationToken);

        // Assert
        executeTaskAfterTheFailure.ShouldBe(TaskStatus.WaitingForActivation);
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        _logger.Messages(LogLevel.Error).ShouldHaveSingleItem().ShouldContain($"{SweepName} retention sweep failed");
    }

    [Fact]
    public async Task StoppingTheHostEndsTheLoopWithoutAnError()
    {
        // Arrange
        _store.Results = [0];

        // Act
        await Target.StartAsync(CancellationToken);
        await _store.LastCallCompleted.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        _logger.Messages(LogLevel.Error).ShouldBeEmpty();
    }

    public interface IProbeStore
    {
        RetentionSweepDeleter? Deleter { get; }
    }

    /// <summary>
    /// A probe store that records every delete call, replays a fixed sequence of results, and can present itself as
    /// unable to delete (a <see langword="null"/> deleter) or as failing.
    /// </summary>
    private sealed class RecordingStore : IProbeStore
    {
        private readonly TaskCompletionSource _lastCallCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<TaskCompletionSource> _adapterCalls = [];
        private readonly List<DateTimeOffset> _observedCutoffs = [];
        private readonly List<int> _observedBatchSizes = [];
        private int _adapterCallCount;
        private int _callCount;

        public IReadOnlyList<int> Results { get; set; } = [0];

        public bool SupportsRetention { get; set; } = true;

        public Exception? ExceptionToThrow { get; set; }

        public Task LastCallCompleted => _lastCallCompleted.Task;

        public IReadOnlyList<DateTimeOffset> ObservedCutoffs => _observedCutoffs;

        public IReadOnlyList<int> ObservedBatchSizes => _observedBatchSizes;

        public int CallCount => _callCount;

        public RetentionSweepDeleter? Deleter
        {
            get
            {
                var call = Interlocked.Increment(ref _adapterCallCount);
                lock (_adapterCalls)
                {
                    foreach (var waiter in _adapterCalls.Take(call))
                    {
                        waiter.TrySetResult();
                    }
                }

                return SupportsRetention ? DeleteAsync : null;
            }
        }

        public Task AdapterCalled(int times)
        {
            lock (_adapterCalls)
            {
                while (_adapterCalls.Count < times)
                {
                    _adapterCalls.Add(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
                }

                var waiter = _adapterCalls[times - 1];
                if (Volatile.Read(ref _adapterCallCount) >= times)
                {
                    waiter.TrySetResult();
                }

                return waiter.Task;
            }
        }

        private Task<int> DeleteAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = Interlocked.Increment(ref _callCount) - 1;
            _observedCutoffs.Add(olderThanUtc);
            _observedBatchSizes.Add(batchSize);

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            var result = index < Results.Count ? Results[index] : Results[^1];
            if (index >= Results.Count - 1)
            {
                _lastCallCompleted.TrySetResult();
            }

            return Task.FromResult(result);
        }
    }

    private sealed class CapturingLogger : ILogger<RetentionSweepService<IProbeStore>>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];
        private readonly Dictionary<LogLevel, TaskCompletionSource> _firstAtLevel = new();

        public List<string> Messages(LogLevel level)
        {
            lock (_entries)
            {
                return _entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();
            }
        }

        public Task Logged(LogLevel level)
        {
            lock (_entries)
            {
                return WaiterFor(level).Task;
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entries)
            {
                _entries.Add((logLevel, formatter(state, exception)));
                WaiterFor(logLevel).TrySetResult();
            }
        }

        private TaskCompletionSource WaiterFor(LogLevel level)
        {
            if (!_firstAtLevel.TryGetValue(level, out var waiter))
            {
                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _firstAtLevel[level] = waiter;
            }

            return waiter;
        }
    }

    /// <summary>
    /// A fake clock that reports when the service under test registers its timer. The generic host runs
    /// <c>BackgroundService.ExecuteAsync</c> on the thread pool, so the periodic timer may not exist yet when
    /// <c>StartAsync</c> returns; advancing the clock before it exists would leave the sweep waiting forever.
    /// </summary>
    private sealed class TimerAwareFakeTimeProvider : FakeTimeProvider
    {
        private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TimerCreated => _timerCreated.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _timerCreated.TrySetResult();
            return timer;
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
