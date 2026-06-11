using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Vulthil.SharedKernel.Outbox;

internal sealed class OutboxProcessor(
    IOutboxStore store,
    IEnumerable<IOutboxDispatcher> dispatchers,
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

            var dispatcher = ResolveDispatcher(outboxMessage.Destination);
            await dispatcher.DispatchAsync(outboxMessage, cancellationToken);

            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to publish outbox message {MessageId}", outboxMessage.Id);
            return exception.ToString();
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private IOutboxDispatcher ResolveDispatcher(OutboxDestination destination) =>
        dispatchers.FirstOrDefault(dispatcher => dispatcher.Handles(destination))
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
    public static readonly string ActivitySourceName = "Vulthil.SharedKernel.Infrastructure";
    /// <summary>
    /// ActivitySource for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
