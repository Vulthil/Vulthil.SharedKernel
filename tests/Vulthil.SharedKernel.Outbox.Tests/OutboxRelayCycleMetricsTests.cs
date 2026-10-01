using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// Groups every test that measures the process-wide <see cref="Telemetry.Meter"/> counters within a narrow
/// listening window, so xUnit never runs them concurrently with each other — a real <see cref="OutboxRelayCycle"/>
/// dispatch anywhere else in the process (e.g. <see cref="OutboxRelayCycleTests"/>) increments the same static
/// counters and would otherwise be misattributed to a concurrently-running measurement.
/// </summary>
[CollectionDefinition(nameof(OutboxTelemetryCollection))]
public sealed class OutboxTelemetryCollection;

[Collection(nameof(OutboxTelemetryCollection))]
public sealed class OutboxRelayCycleMetricsTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxRelayCycle> _lazyTarget;

    private OutboxRelayCycle Target => _lazyTarget.Value;

    public OutboxRelayCycleMetricsTests()
    {
        _lazyTarget = new(CreateInstance<OutboxRelayCycle>);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
        Use<IOutboxStore>(new InMemoryRelayStore(messageCount: 1));
    }

    [Fact]
    public async Task RelayingAMessageIncrementsTheRelayedCounter()
    {
        // Arrange
        SetupDispatcher(succeeds: true);

        // Act
        var count = await MeasureCounterAsync("vulthil.outbox.relayed", () => Target.RunAsync(CancellationToken));

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task AFailedDispatchIncrementsTheFailedCounter()
    {
        // Arrange
        SetupDispatcher(succeeds: false);

        // Act
        var count = await MeasureCounterAsync("vulthil.outbox.failed", () => Target.RunAsync(CancellationToken));

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task CancellingMidBatchDoesNotIncrementTheFailedCounterOrLogAnError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        SetupDispatcherToThrowOperationCanceled();

        // Act
        var count = await MeasureCounterAsync("vulthil.outbox.failed", () =>
            Should.ThrowAsync<OperationCanceledException>(() => Target.RunAsync(cts.Token)));

        // Assert
        count.ShouldBe(0);
        GetMock<ILogger<OutboxRelayCycle>>().Invocations.ShouldBeEmpty();
    }

    private void SetupDispatcherToThrowOperationCanceled()
    {
        var dispatcher = GetMock<IOutboxDispatcher>();
        dispatcher.Setup(d => d.Handles(It.IsAny<OutboxDestination>())).Returns(true);
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<OutboxMessageData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        GetMock<IServiceProvider>()
            .Setup(sp => sp.GetService(typeof(IEnumerable<IOutboxDispatcher>)))
            .Returns(new[] { dispatcher.Object });
    }

    private void SetupDispatcher(bool succeeds)
    {
        var dispatcher = GetMock<IOutboxDispatcher>();
        dispatcher.Setup(d => d.Handles(It.IsAny<OutboxDestination>())).Returns(true);
        var dispatch = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<OutboxMessageData>(), It.IsAny<CancellationToken>()));
        if (succeeds)
        {
            dispatch.Returns(Task.CompletedTask);
        }
        else
        {
            dispatch.ThrowsAsync(new InvalidOperationException("publish failed"));
        }

        GetMock<IServiceProvider>()
            .Setup(sp => sp.GetService(typeof(IEnumerable<IOutboxDispatcher>)))
            .Returns(new[] { dispatcher.Object });
    }

    private static async Task<long> MeasureCounterAsync(string instrumentName, Func<Task> action)
    {
        long total = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == Telemetry.MeterName && instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => total += measurement);
        listener.Start();

        await action();

        return total;
    }
}
