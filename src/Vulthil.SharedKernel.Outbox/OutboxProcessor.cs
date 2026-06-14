using System.Diagnostics;
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
    public static readonly string ActivitySourceName = "Vulthil.SharedKernel.Infrastructure";
    /// <summary>
    /// ActivitySource for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
