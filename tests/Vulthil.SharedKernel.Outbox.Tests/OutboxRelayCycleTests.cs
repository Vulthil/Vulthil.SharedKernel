using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Application;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Events;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// Shares <see cref="OutboxTelemetryCollection"/> with <see cref="OutboxRelayCycleMetricsTests"/>: every test here
/// drives a real <see cref="OutboxRelayCycle"/> to a successful or failed dispatch, incrementing the same
/// process-wide <see cref="Telemetry"/> counters that class measures within a narrow window.
/// </summary>
[Collection(nameof(OutboxTelemetryCollection))]
public sealed class OutboxRelayCycleTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxRelayCycle> _lazyTarget;

    private OutboxRelayCycle Target => _lazyTarget.Value;

    public OutboxRelayCycleTests()
    {
        _lazyTarget = new(CreateInstance<OutboxRelayCycle>);
        UseOptions(new OutboxProcessingOptions());
    }

    [Fact]
    public async Task AnEmptyClaimRecordsNothing()
    {
        // Arrange
        var store = UseStore(new InMemoryRelayStore(messageCount: 0));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.ShouldBe(new OutboxRelayCycleResult(Claimed: 0, Relayed: 0));
        store.RecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task ClaimsAndRecordsWithTheConfiguredBatchSizeAndRetryLimit()
    {
        // Arrange
        UseOptions(new OutboxProcessingOptions { BatchSize = 5, MaxRetries = 7 });
        UseRootDispatchers(new RecordingDispatcher());
        var store = UseStore(new InMemoryRelayStore(messageCount: 2));

        // Act
        await Target.RunAsync(CancellationToken);

        // Assert
        store.Claims.ShouldHaveSingleItem().ShouldBe((5, 7));
        store.RecordCount.ShouldBe(1);
        store.RecordedMaxRetries.ShouldBe(7);
    }

    [Fact]
    public async Task InSequenceEachMessageIsDispatchedInTheRelayScopeAsAStepOfTheUnit()
    {
        // Arrange
        UseOptions(new OutboxProcessingOptions { EnableParallelPublishing = false });
        var dispatcher = new RecordingDispatcher();
        UseRootDispatchers(dispatcher);
        var store = UseStore(new InMemoryRelayStore(messageCount: 3));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.ShouldBe(new OutboxRelayCycleResult(Claimed: 3, Relayed: 3));
        dispatcher.CallCount.ShouldBe(3);
        store.StepCount.ShouldBe(3);
        store.RelayedIds.ShouldBe(store.Messages.Select(message => message.Id));
        GetMock<IServiceScopeFactory>().Verify(factory => factory.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task InParallelEachMessageIsDispatchedInItsOwnScopeOutsideTheUnitsSteps()
    {
        // Arrange
        const int messageCount = 3;
        UseOptions(new OutboxProcessingOptions { EnableParallelPublishing = true });
        var relayScope = GetMock<IServiceProvider>();
        var scopedDispatchers = new ConcurrentBag<RecordingDispatcher>();
        var scopeFactory = new RecordingServiceScopeFactory(() =>
        {
            var dispatcher = new RecordingDispatcher();
            scopedDispatchers.Add(dispatcher);
            return new SingleDispatcherServiceProvider(dispatcher);
        });
        Use<IServiceScopeFactory>(scopeFactory);
        var store = UseStore(new InMemoryRelayStore(messageCount));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.Relayed.ShouldBe(messageCount);
        store.StepCount.ShouldBe(0);
        scopeFactory.ScopesCreated.ShouldBe(messageCount);
        scopedDispatchers.ShouldAllBe(dispatcher => dispatcher.CallCount == 1);
        relayScope.Verify(provider => provider.GetService(It.IsAny<Type>()), Times.Never);
    }

    [Fact]
    public async Task InParallelAtMostMaxDegreeOfParallelismDispatchesRunAtOnce()
    {
        // Arrange
        const int maxDegreeOfParallelism = 2;
        const int messageCount = 6;
        UseOptions(new OutboxProcessingOptions
        {
            EnableParallelPublishing = true,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
        });
        var dispatcher = new GatedDispatcher(saturationCount: maxDegreeOfParallelism);
        Use<IServiceScopeFactory>(new RecordingServiceScopeFactory(() => new SingleDispatcherServiceProvider(dispatcher)));
        UseStore(new InMemoryRelayStore(messageCount));

        // Act
        var run = Target.RunAsync(CancellationToken);
        await dispatcher.SaturationReached.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);
        dispatcher.Release();
        var cycle = await run;

        // Assert
        cycle.Relayed.ShouldBe(messageCount);
        dispatcher.PeakConcurrency.ShouldBe(maxDegreeOfParallelism);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFailedDispatchIsRecordedWithItsErrorAndTheOtherMessagesAreRelayed(bool enableParallelPublishing)
    {
        // Arrange
        UseOptions(new OutboxProcessingOptions { EnableParallelPublishing = enableParallelPublishing });
        var messages = InMemoryRelayStore.CreateMessages(3);
        var failingMessageId = messages[1].Id;
        var dispatcher = new FailingForOneMessageDispatcher(failingMessageId);
        UseRootDispatchers(dispatcher);
        Use<IServiceScopeFactory>(new RecordingServiceScopeFactory(() => new SingleDispatcherServiceProvider(dispatcher)));
        var store = UseStore(new InMemoryRelayStore(messages));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.ShouldBe(new OutboxRelayCycleResult(Claimed: 3, Relayed: 2));
        store.RelayedIds.ShouldBe([messages[0].Id, messages[2].Id]);
        var failure = store.Failures.ShouldHaveSingleItem();
        failure.Id.ShouldBe(failingMessageId);
        failure.Error.ShouldContain("Simulated dispatch failure.");
    }

    [Fact]
    public async Task InSequenceAFailedDispatchFailsItsStepSoTheStoreCanUndoIt()
    {
        // Arrange
        var messages = InMemoryRelayStore.CreateMessages(3);
        UseRootDispatchers(new FailingForOneMessageDispatcher(messages[1].Id));
        var store = UseStore(new InMemoryRelayStore(messages));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.ShouldBe(new OutboxRelayCycleResult(Claimed: 3, Relayed: 2));
        store.StepCount.ShouldBe(3);
        store.FailedStepCount.ShouldBe(1);
        store.Failures.ShouldHaveSingleItem().Id.ShouldBe(messages[1].Id);
    }

    [Fact]
    public async Task AMessageWithoutADispatcherForItsDestinationIsRecordedAsAFailure()
    {
        // Arrange
        UseRootDispatchers();
        var store = UseStore(new InMemoryRelayStore(messageCount: 1));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.Relayed.ShouldBe(0);
        store.Failures.ShouldHaveSingleItem().Error.ShouldContain($"No {nameof(IOutboxDispatcher)} is registered for outbox destination '{OutboxDestination.DomainEvent}'.");
    }

    [Fact]
    public async Task ACancellationThatIsNotTheCyclesIsRecordedAsAFailure()
    {
        // Arrange
        UseRootDispatchers(new TimingOutDispatcher());
        var store = UseStore(new InMemoryRelayStore(messageCount: 1));

        // Act
        var cycle = await Target.RunAsync(CancellationToken);

        // Assert
        cycle.Relayed.ShouldBe(0);
        store.Failures.ShouldHaveSingleItem().Error.ShouldContain(nameof(TaskCanceledException));
    }

    [Fact]
    public async Task ACancellationOfTheCycleAbandonsTheUnitWithoutRecording()
    {
        // Arrange
        using var cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        UseRootDispatchers(new CancelingDispatcher(cycleCancellation));
        var store = UseStore(new InMemoryRelayStore(messageCount: 2));

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Target.RunAsync(cycleCancellation.Token));

        // Assert
        store.RecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task ACancelledDomainEventDispatchStopsTheCycleWithoutRecordingAFailure()
    {
        // Arrange
        using var cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        var probe = new CancellingHandlerProbe(cycleCancellation);
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddHandlers(handlers => handlers.RegisterHandlerAssemblies(typeof(OutboxRelayCycleTests).Assembly));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        UseRootDispatchers(new DomainEventOutboxDispatcher(scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>()));
        var store = UseStore(new InMemoryRelayStore([CancellingEventMessage(), CancellingEventMessage()]));

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => Target.RunAsync(cycleCancellation.Token));

        // Assert
        probe.Calls.ShouldBe(1);
        store.RecordCount.ShouldBe(0);
        GetMock<ILogger<OutboxRelayCycle>>().Invocations.ShouldBeEmpty();
    }

    private void UseOptions(OutboxProcessingOptions options) => Use<IOptions<OutboxProcessingOptions>>(Options.Create(options));

    private InMemoryRelayStore UseStore(InMemoryRelayStore store)
    {
        Use<IOutboxStore>(store);
        return store;
    }

    private void UseRootDispatchers(params IOutboxDispatcher[] dispatchers) =>
        GetMock<IServiceProvider>().Setup(provider => provider.GetService(typeof(IEnumerable<IOutboxDispatcher>))).Returns(dispatchers);

    private static OutboxMessageData CancellingEventMessage() =>
        new(Guid.NewGuid(), typeof(CancellingEvent).AssemblyQualifiedName!, "{}", null, null, OutboxDestination.DomainEvent, null);

    public sealed record CancellingEvent : IDomainEvent;

    /// <summary>Counts the handler's calls and holds the cancellation the handler fires, like a host stopping mid-batch.</summary>
    public sealed class CancellingHandlerProbe(CancellationTokenSource cycleCancellation)
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public CancellationTokenSource CycleCancellation { get; } = cycleCancellation;

        public void RecordCall() => Interlocked.Increment(ref _calls);
    }

    public sealed class CancellingEventHandler(CancellingHandlerProbe probe) : IDomainEventHandler<CancellingEvent>
    {
        public async Task HandleAsync(CancellingEvent notification, CancellationToken cancellationToken = default)
        {
            probe.RecordCall();
            await probe.CycleCancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class RecordingServiceScopeFactory(Func<IServiceProvider> scopedProviderFactory) : IServiceScopeFactory
    {
        private int _scopesCreated;

        public int ScopesCreated => _scopesCreated;

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _scopesCreated);
            return new FakeScope(scopedProviderFactory());
        }

        private sealed class FakeScope(IServiceProvider serviceProvider) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = serviceProvider;

            public void Dispose()
            {
            }
        }
    }

    /// <summary>Resolves a single, fixed <see cref="IOutboxDispatcher"/> — stands in for one isolated DI scope's container.</summary>
    private sealed class SingleDispatcherServiceProvider(IOutboxDispatcher dispatcher) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IEnumerable<IOutboxDispatcher>) ? new[] { dispatcher } : null;
    }

    private sealed class RecordingDispatcher : IOutboxDispatcher
    {
        private int _callCount;

        public int CallCount => _callCount;

        public bool Handles(OutboxDestination destination) => true;

        public Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Holds every dispatch at a gate until the test releases it, and signals once <c>saturationCount</c> dispatches
    /// are held, so the test observes the peak concurrency the cycle allowed without depending on timing.
    /// </summary>
    private sealed class GatedDispatcher(int saturationCount) : IOutboxDispatcher
    {
        private readonly TaskCompletionSource _saturated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _inFlight;
        private int _peak;
        private int _arrivals;

        public Task SaturationReached => _saturated.Task;

        public int PeakConcurrency => Volatile.Read(ref _peak);

        public bool Handles(OutboxDestination destination) => true;

        public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
        {
            RecordArrival();
            await _gate.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _inFlight);
        }

        public void Release() => _gate.TrySetResult();

        private void RecordArrival()
        {
            UpdatePeak(Interlocked.Increment(ref _inFlight));
            if (Interlocked.Increment(ref _arrivals) >= saturationCount)
            {
                _saturated.TrySetResult();
            }
        }

        private void UpdatePeak(int current)
        {
            var seen = Volatile.Read(ref _peak);
            while (current > seen)
            {
                var previous = Interlocked.CompareExchange(ref _peak, current, seen);
                if (previous == seen)
                {
                    return;
                }

                seen = previous;
            }
        }
    }

    private sealed class FailingForOneMessageDispatcher(Guid failingMessageId) : IOutboxDispatcher
    {
        public bool Handles(OutboxDestination destination) => true;

        public Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken) =>
            message.Id == failingMessageId
                ? throw new InvalidOperationException("Simulated dispatch failure.")
                : Task.CompletedTask;
    }

    /// <summary>Fails like a transport whose own timeout fired, while the relay cycle itself is not canceled.</summary>
    private sealed class TimingOutDispatcher : IOutboxDispatcher
    {
        public bool Handles(OutboxDestination destination) => true;

        public Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("Simulated transport timeout.");
    }

    /// <summary>Cancels the relay cycle from inside its first dispatch, the way a host stopping mid-batch does.</summary>
    private sealed class CancelingDispatcher(CancellationTokenSource cycleCancellation) : IOutboxDispatcher
    {
        public bool Handles(OutboxDestination destination) => true;

        public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
        {
            await cycleCancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
