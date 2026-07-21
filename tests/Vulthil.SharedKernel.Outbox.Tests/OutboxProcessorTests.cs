using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// Shares <see cref="OutboxTelemetryCollection"/> with <see cref="OutboxProcessorMetricsTests"/>: every test here
/// drives a real <see cref="OutboxProcessor"/> to a successful or failed dispatch, incrementing the same
/// process-wide <see cref="Telemetry"/> counters that class measures within a narrow window.
/// </summary>
[Collection(nameof(OutboxTelemetryCollection))]
public sealed class OutboxProcessorTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxProcessor> _lazyTarget;

    private OutboxProcessor Target => _lazyTarget.Value;

    public OutboxProcessorTests() => _lazyTarget = new(CreateInstance<OutboxProcessor>);

    [Fact]
    public async Task ParallelPublishingDisabledDispatchesOnTheRootServiceProviderWithoutCreatingAScope()
    {
        // Arrange
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions { EnableParallelPublishing = false }));
        var dispatcher = new RecordingDispatcher();
        GetMock<IServiceProvider>().Setup(sp => sp.GetService(typeof(IEnumerable<IOutboxDispatcher>))).Returns(new IOutboxDispatcher[] { dispatcher });
        Use<IOutboxStore>(new FakeParallelOutboxStore(Messages(1), maxDegreeOfParallelism: null));

        // Act
        var processed = await Target.ExecuteAsync(CancellationToken);

        // Assert
        processed.ShouldBe(1);
        dispatcher.CallCount.ShouldBe(1);
        GetMock<IServiceScopeFactory>().Verify(factory => factory.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task ParallelPublishingEnabledDispatchesEachMessageOnAnIsolatedDependencyInjectionScope()
    {
        // Arrange
        const int messageCount = 3;
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions { EnableParallelPublishing = true }));
        var scopedDispatchers = new ConcurrentBag<RecordingDispatcher>();
        var scopeFactory = new RecordingServiceScopeFactory(() =>
        {
            var dispatcher = new RecordingDispatcher();
            scopedDispatchers.Add(dispatcher);
            return new SingleDispatcherServiceProvider(dispatcher);
        });
        Use<IServiceScopeFactory>(scopeFactory);
        Use<IOutboxStore>(new FakeParallelOutboxStore(Messages(messageCount), messageCount));

        // Act
        var processed = await Target.ExecuteAsync(CancellationToken);

        // Assert
        processed.ShouldBe(messageCount);
        scopeFactory.ScopesCreated.ShouldBe(messageCount);
        scopedDispatchers.Count.ShouldBe(messageCount);
        scopedDispatchers.ShouldAllBe(dispatcher => dispatcher.CallCount == 1);
    }

    [Fact]
    public async Task MaxDegreeOfParallelismBoundsTheNumberOfConcurrentDispatches()
    {
        // Arrange
        const int maxDegreeOfParallelism = 2;
        const int messageCount = 6;
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions
        {
            EnableParallelPublishing = true,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
        }));
        var dispatcher = new ConcurrencyTrackingDispatcher();
        Use<IServiceScopeFactory>(new RecordingServiceScopeFactory(() => new SingleDispatcherServiceProvider(dispatcher)));
        Use<IOutboxStore>(new FakeParallelOutboxStore(Messages(messageCount), maxDegreeOfParallelism));

        // Act
        var processed = await Target.ExecuteAsync(CancellationToken);

        // Assert
        processed.ShouldBe(messageCount);
        dispatcher.MaxObservedConcurrency.ShouldBeLessThanOrEqualTo(maxDegreeOfParallelism);
        dispatcher.MaxObservedConcurrency.ShouldBe(maxDegreeOfParallelism);
    }

    [Fact]
    public async Task AFailingParallelDispatchDoesNotCorruptTheOtherDispatchesOutcomes()
    {
        // Arrange
        var messages = Messages(3);
        var failingMessageId = messages[1].Id;
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions
        {
            EnableParallelPublishing = true,
            MaxDegreeOfParallelism = 3,
        }));
        Use<IServiceScopeFactory>(new RecordingServiceScopeFactory(() => new SingleDispatcherServiceProvider(new FailingForOneMessageDispatcher(failingMessageId))));
        var store = new FakeParallelOutboxStore(messages, 3);
        Use<IOutboxStore>(store);

        // Act
        var processed = await Target.ExecuteAsync(CancellationToken);

        // Assert
        processed.ShouldBe(2);
        store.Outcomes[failingMessageId].ShouldNotBeNull();
        store.Outcomes.Where(outcome => outcome.Key != failingMessageId).ShouldAllBe(outcome => outcome.Value == null);
    }

    private static List<OutboxMessageData> Messages(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new OutboxMessageData(Guid.NewGuid(), "Some.Event", "{}", null, null, OutboxDestination.DomainEvent, null))
            .ToList();

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

    /// <summary>Tracks concurrent in-flight <see cref="DispatchAsync"/> calls to observe the peak concurrency a caller allowed.</summary>
    private sealed class ConcurrencyTrackingDispatcher : IOutboxDispatcher
    {
        private int _active;
        private int _maxObservedConcurrency;

        public int MaxObservedConcurrency => _maxObservedConcurrency;

        public bool Handles(OutboxDestination destination) => true;

        public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _active);
            TrackMax(current);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void TrackMax(int observed)
        {
            int current;
            do
            {
                current = Volatile.Read(ref _maxObservedConcurrency);
                if (observed <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _maxObservedConcurrency, observed, current) != current);
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

    /// <summary>
    /// Fake <see cref="IOutboxStore"/> that mirrors <c>EntityFrameworkOutboxStore</c>'s
    /// <c>EnableParallelPublishing</c>/<c>MaxDegreeOfParallelism</c> semaphore-throttled dispatch, so
    /// <see cref="OutboxProcessor"/> can be exercised under the same concurrency contract the real store applies
    /// without depending on the EF Core store project.
    /// </summary>
    private sealed class FakeParallelOutboxStore(IReadOnlyList<OutboxMessageData> messages, int? maxDegreeOfParallelism) : IOutboxStore
    {
        public ConcurrentDictionary<Guid, string?> Outcomes { get; } = new();

        public bool IsInTransaction => false;

        public void AddOutboxMessage(OutboxMessage message)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public async Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken)
        {
            if (maxDegreeOfParallelism is not { } max)
            {
                var successCount = 0;
                foreach (var message in messages)
                {
                    var error = await dispatch(message, cancellationToken);
                    Outcomes[message.Id] = error;
                    successCount += error is null ? 1 : 0;
                }

                return successCount;
            }

            using var throttle = new SemaphoreSlim(max);
            var results = await Task.WhenAll(messages.Select(message => DispatchThrottledAsync(message, dispatch, throttle, cancellationToken)));
            foreach (var (id, error) in results)
            {
                Outcomes[id] = error;
            }

            return results.Count(result => result.Error is null);
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
    }
}
