using Microsoft.Extensions.DependencyInjection;
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
        await gate.Entered;

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
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
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
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
