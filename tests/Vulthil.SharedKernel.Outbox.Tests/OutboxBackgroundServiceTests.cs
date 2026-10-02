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
