using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.SharedKernel.Outbox;

internal sealed class OutboxProcessor(
    IOutboxStore store,
    IServiceProvider serviceProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessingOptions> options,
    ILogger<OutboxProcessor> logger)
{
    internal Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        store.ProcessBatchAsync(DispatchAsync, cancellationToken);

    private async Task<string?> DispatchAsync(OutboxMessageData outboxMessage, CancellationToken cancellationToken)
    {
        Activity? activity = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(outboxMessage.TraceParent))
            {
                var parent = ActivityContext.Parse(outboxMessage.TraceParent, outboxMessage.TraceState);
                activity = Telemetry.ActivitySource.StartActivity("OutboxPublishing", ActivityKind.Producer, parent);
            }

            if (options.Value.EnableParallelPublishing)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await DispatchInScopeAsync(scope.ServiceProvider, outboxMessage, cancellationToken);
            }
            else
            {
                await DispatchInScopeAsync(serviceProvider, outboxMessage, cancellationToken);
            }

            Telemetry.Relayed.Add(1);
            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to publish outbox message {MessageId}", outboxMessage.Id);
            Telemetry.Failed.Add(1);
            return exception.ToString();
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private static Task DispatchInScopeAsync(IServiceProvider services, OutboxMessageData outboxMessage, CancellationToken cancellationToken)
    {
        var dispatcher = ResolveDispatcher(services, outboxMessage.Destination);
        return dispatcher.DispatchAsync(outboxMessage, cancellationToken);
    }

    private static IOutboxDispatcher ResolveDispatcher(IServiceProvider services, OutboxDestination destination) =>
        services.GetServices<IOutboxDispatcher>().FirstOrDefault(dispatcher => dispatcher.Handles(destination))
            ?? throw new InvalidOperationException($"No {nameof(IOutboxDispatcher)} is registered for outbox destination '{destination}'.");
}

/// <summary>
/// Static class for holding the Telemetry ActivitySource used for all outbox processing operations, enabling consistent correlation of telemetry across the capture, processing, and publishing stages.
/// </summary>
public static class Telemetry
{
    /// <summary>
    /// ActivitySourceName for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    public static string ActivitySourceName => "Vulthil.SharedKernel.Outbox";
    /// <summary>
    /// ActivitySource for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Meter name for outbox relay metrics. Subscribe to it on a <c>MeterProviderBuilder</c> with
    /// <c>AddVulthilOutboxInstrumentation()</c>.
    /// </summary>
    public static string MeterName => "Vulthil.SharedKernel.Outbox";
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> Relayed = Meter.CreateCounter<long>(
        "vulthil.outbox.relayed", unit: "{message}", description: "Outbox messages successfully relayed.");
    internal static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "vulthil.outbox.failed", unit: "{message}", description: "Outbox message relay attempts that failed.");
}
