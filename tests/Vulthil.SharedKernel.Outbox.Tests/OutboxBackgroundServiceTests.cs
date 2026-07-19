using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

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
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
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
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
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
        Use<IEnumerable<IOutboxRelayGate>>([]);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new CountingOutboxStore(result: batchSize - 3);
        Use<IOutboxStore>(store);
        Use(CreateInstance<OutboxProcessor>());
        TimeSpan? capturedDelay = null;
        GetMock<IOutboxSignal>()
            .Setup(signal => signal.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((delay, _) => capturedDelay ??= delay)
            .Returns(Task.CompletedTask);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.SecondCallStarted;
        await Target.StopAsync(CancellationToken);

        // Assert
        capturedDelay.ShouldNotBeNull();
        capturedDelay!.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(baseDelaySeconds));
    }

    [Fact]
    public async Task AFullySuccessfulFullBatchRefetchesImmediately()
    {
        // Arrange
        const int batchSize = 10;
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions { BatchSize = batchSize }));
        Use<IEnumerable<IOutboxRelayGate>>([]);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new CountingOutboxStore(result: batchSize);
        Use<IOutboxStore>(store);
        Use(CreateInstance<OutboxProcessor>());

        // Act
        await Target.StartAsync(CancellationToken);
        await store.SecondCallStarted;
        await Target.StopAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private sealed class CountingOutboxStore(int result) : IOutboxStore
    {
        private readonly TaskCompletionSource _secondCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public Task SecondCallStarted => _secondCallStarted.Task;

        public bool IsInTransaction => false;

        public void AddOutboxMessage(OutboxMessage message)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _callCount) == 2)
            {
                _secondCallStarted.TrySetResult();
            }

            return Task.FromResult(result);
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
