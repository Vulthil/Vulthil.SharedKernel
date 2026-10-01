using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// Shares <see cref="OutboxTelemetryCollection"/> with <see cref="OutboxRelayCycleMetricsTests"/>: the back-off tests
/// run real <see cref="OutboxRelayCycle"/>s that increment the process-wide <see cref="Telemetry"/> counters that class
/// measures within a narrow window.
/// </summary>
[Collection(nameof(OutboxTelemetryCollection))]
public sealed class OutboxBackgroundServiceTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxBackgroundService> _lazyTarget;

    private OutboxBackgroundService Target => _lazyTarget.Value;

    public OutboxBackgroundServiceTests()
    {
        _lazyTarget = new(CreateInstance<OutboxBackgroundService>);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
    }

    [Fact]
    public async Task StoppingWhileWaitingForRelayGatesCompletesGracefully()
    {
        // Arrange
        var gate = new BlockingRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StoppingBeforeTheRelayLoopHasRunCompletesGracefully()
    {
        // Arrange
        Use<IEnumerable<IOutboxRelayGate>>([new BlockingRelayGate()]);
        await Target.StartAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StartingWithAnAlreadyCanceledTokenCompletesTheExecuteTaskGracefully()
    {
        // Arrange
        Use<IEnumerable<IOutboxRelayGate>>([new BlockingRelayGate()]);
        using var canceledTokenSource = new CancellationTokenSource();
        await canceledTokenSource.CancelAsync();

        // Act
        await Target.StartAsync(canceledTokenSource.Token);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task RestartingAfterStopRunsTheRelayAgain()
    {
        // Arrange
        var gate = new BlockingRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);
        var firstExecuteTask = Target.ExecuteTask;

        // Act
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask.ShouldNotBeSameAs(firstExecuteTask);
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StartingWhileThePreviousExecuteTaskIsStillRunningWaitsForItInsteadOfOverlapping()
    {
        // Arrange — a stop bounded by a token so short it elapses before the loop (deliberately unresponsive to
        // cancellation, unlike BlockingRelayGate) can notice it, mirroring how a timed ResetAsync stop can return
        // while the execute task it waited for is still winding down.
        var gate = new UnresponsiveRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Target.StopAsync(stopTimeout.Token);
        var firstExecuteTask = Target.ExecuteTask;

        // Act
        var startTask = Target.StartAsync(CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        var stillWaitingForThePreviousLoop = !startTask.IsCompleted;
        gate.Release();
        await startTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        stillWaitingForThePreviousLoop.ShouldBeTrue();
        firstExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        Target.ExecuteTask.ShouldNotBeSameAs(firstExecuteTask);
    }

    [Fact]
    public async Task StoppingAfterDisposeCompletesGracefully()
    {
        // Arrange
        var gate = new BlockingRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);
        Target.Dispose();

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StoppingTwiceCompletesGracefully()
    {
        // Arrange
        var gate = new BlockingRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task DisposingTwiceIsSafe()
    {
        // Arrange
        var gate = new BlockingRelayGate();
        Use<IEnumerable<IOutboxRelayGate>>([gate]);
        await Target.StartAsync(CancellationToken);
        await gate.WaitForEntryAsync(CancellationToken);

        // Act
        Target.Dispose();
        Target.Dispose();

        // Assert
        await Target.ExecuteTask!;
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StoppingAndRestartingConcurrentlyConvergesCleanly()
    {
        // Arrange
        Use<IEnumerable<IOutboxRelayGate>>([new BlockingRelayGate()]);
        await Target.StartAsync(CancellationToken);

        // Act
        for (var i = 0; i < 300; i++)
        {
            using var startSignal = new SemaphoreSlim(0, 2);
            var stopTask = Task.Run(
                async () =>
                {
                    await startSignal.WaitAsync(CancellationToken);
                    await Target.StopAsync(CancellationToken);
                },
                CancellationToken);
            var startTask = Task.Run(
                async () =>
                {
                    await startSignal.WaitAsync(CancellationToken);
                    await Target.StartAsync(CancellationToken);
                },
                CancellationToken);
            startSignal.Release(2);
            await Task.WhenAll(stopTask, startTask).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            await Target.StopAsync(CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            await Target.StartAsync(CancellationToken);
        }

        await Target.StopAsync(CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task AFaultOutsideTheProcessingLoopStopsTheApplication()
    {
        // Arrange
        Use<IEnumerable<IOutboxRelayGate>>([]);
        var failingOptions = new Mock<IOptions<OutboxProcessingOptions>>();
        failingOptions.Setup(o => o.Value).Throws(new InvalidOperationException("Options failed"));
        Use(failingOptions.Object);

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        GetMock<IHostApplicationLifetime>().Verify(lifetime => lifetime.StopApplication(), Times.Once);
    }

    [Fact]
    public async Task ABatchWithFewerSuccessesThanTheBatchSizeDelaysTheNextFetch()
    {
        // Arrange
        const int batchSize = 10;
        const int baseDelaySeconds = 2;
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions
        {
            BatchSize = batchSize,
            OutboxProcessingDelaySeconds = baseDelaySeconds
        }));
        var store = UseRelayCycleOver(new InMemoryRelayStore(messageCount: batchSize - 3));
        TimeSpan? capturedDelay = null;
        GetMock<IOutboxSignal>()
            .Setup(signal => signal.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((delay, _) => capturedDelay ??= delay)
            .Returns(Task.CompletedTask);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.SecondRunStarted.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        capturedDelay.ShouldNotBeNull();
        capturedDelay.Value.ShouldBe(TimeSpan.FromSeconds(baseDelaySeconds));
    }

    [Fact]
    public async Task AFullySuccessfulFullBatchRefetchesImmediately()
    {
        // Arrange
        const int batchSize = 10;
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions { BatchSize = batchSize }));
        var store = UseRelayCycleOver(new InMemoryRelayStore(messageCount: batchSize));

        // Act
        await Target.StartAsync(CancellationToken);
        await store.SecondRunStarted.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Registers a real <see cref="OutboxRelayCycle"/> over <paramref name="store"/> that every relay scope resolves,
    /// with a dispatcher that delivers every message.
    /// </summary>
    private InMemoryRelayStore UseRelayCycleOver(InMemoryRelayStore store)
    {
        Use<IEnumerable<IOutboxRelayGate>>([]);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<IOutboxStore>(store);
        GetMock<IServiceProvider>()
            .Setup(provider => provider.GetService(typeof(IEnumerable<IOutboxDispatcher>)))
            .Returns(new IOutboxDispatcher[] { new AcceptingDispatcher() });
        Use(CreateInstance<OutboxRelayCycle>());
        return store;
    }

    private sealed class BlockingRelayGate : IOutboxRelayGate
    {
        private readonly Channel<bool> _entries = Channel.CreateUnbounded<bool>();

        public async Task WaitForEntryAsync(CancellationToken cancellationToken) => await _entries.Reader.ReadAsync(cancellationToken);

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            _entries.Writer.TryWrite(true);
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    /// <summary>
    /// A relay gate that, unlike <see cref="BlockingRelayGate"/>, does not observe the stopping token at all — it only
    /// unblocks when the test calls <see cref="Release"/>. Simulates a loop that has not yet noticed its own
    /// cancellation, so a stop bounded by a short timeout returns while the loop is still running.
    /// </summary>
    private sealed class UnresponsiveRelayGate : IOutboxRelayGate
    {
        private readonly Channel<bool> _entries = Channel.CreateUnbounded<bool>();
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitForEntryAsync(CancellationToken cancellationToken) => await _entries.Reader.ReadAsync(cancellationToken);

        public void Release() => _release.TrySetResult();

        public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            _entries.Writer.TryWrite(true);
            await _release.Task;
        }
    }

    private sealed class AcceptingDispatcher : IOutboxDispatcher
    {
        public bool Handles(OutboxDestination destination) => true;

        public Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken) => Task.CompletedTask;
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
