using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Runs one relay cycle — claim a batch, dispatch each message, record the outcomes — inside one relay unit of the
/// store (see <see cref="IOutboxStore.RunRelayUnitAsync{TResult}"/>). It owns the dispatch rules:
/// <list type="bullet">
/// <item><description>Each message goes to the <see cref="IOutboxDispatcher"/> that handles its destination.</description></item>
/// <item><description>In sequence, a message is dispatched in the relay's own scope as a step of the unit (see
/// <see cref="IOutboxRelayUnit.RunStepAsync"/>), so a handler's writes and its transactional publishes join the relay
/// transaction and commit with the batch, and a failed handler's work is undone without ending the transaction.</description></item>
/// <item><description>With <see cref="OutboxProcessingOptions.EnableParallelPublishing"/>, each message is dispatched in its
/// own scope, so handlers never share the relay's <c>DbContext</c>, and at most
/// <see cref="OutboxProcessingOptions.MaxDegreeOfParallelism"/> dispatches run at once.</description></item>
/// <item><description>A failed dispatch never stops the batch: its error is recorded and the message is retried by a later
/// cycle.</description></item>
/// </list>
/// </summary>
internal sealed class OutboxRelayCycle(
    IOutboxStore store,
    IServiceProvider serviceProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessingOptions> options,
    ILogger<OutboxRelayCycle> logger)
{
    internal Task<OutboxRelayCycleResult> RunAsync(CancellationToken cancellationToken) =>
        store.RunRelayUnitAsync(RunUnitAsync, cancellationToken);

    private async Task<OutboxRelayCycleResult> RunUnitAsync(IOutboxRelayUnit unit, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var batch = await unit.ClaimAsync(settings.BatchSize, settings.MaxRetries, cancellationToken).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            return new OutboxRelayCycleResult(Claimed: 0, Relayed: 0);
        }

        var errors = settings.EnableParallelPublishing
            ? await DispatchInParallelAsync(batch, settings.MaxDegreeOfParallelism, cancellationToken).ConfigureAwait(false)
            : await DispatchInSequenceAsync(unit, batch, cancellationToken).ConfigureAwait(false);

        var relayedIds = new List<Guid>(batch.Count);
        var failures = new List<OutboxMessageFailure>();
        for (var index = 0; index < batch.Count; index++)
        {
            if (errors[index] is { } error)
            {
                failures.Add(new OutboxMessageFailure(batch[index].Id, error));
            }
            else
            {
                relayedIds.Add(batch[index].Id);
            }
        }

        await unit.RecordAsync(relayedIds, failures, settings.MaxRetries, cancellationToken).ConfigureAwait(false);
        return new OutboxRelayCycleResult(batch.Count, relayedIds.Count);
    }

    private async Task<string?[]> DispatchInSequenceAsync(IOutboxRelayUnit unit, IReadOnlyList<OutboxMessageData> batch, CancellationToken cancellationToken)
    {
        var errors = new string?[batch.Count];
        for (var index = 0; index < batch.Count; index++)
        {
            errors[index] = await DispatchAsync(serviceProvider, batch[index], unit, cancellationToken).ConfigureAwait(false);
        }

        return errors;
    }

    private async Task<string?[]> DispatchInParallelAsync(IReadOnlyList<OutboxMessageData> batch, int maxDegreeOfParallelism, CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(maxDegreeOfParallelism);
        return await Task.WhenAll(batch.Select(message => DispatchInOwnScopeAsync(message, throttle, cancellationToken))).ConfigureAwait(false);
    }

    private async Task<string?> DispatchInOwnScopeAsync(OutboxMessageData message, SemaphoreSlim throttle, CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var scope = scopeFactory.CreateAsyncScope();
            await using var _ = scope.ConfigureAwait(false);
            return await DispatchAsync(scope.ServiceProvider, message, unit: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            throttle.Release();
        }
    }

    /// <summary>
    /// Dispatches one message and returns <see langword="null"/> when it was delivered, or the error to record against
    /// it. A cancellation of the cycle itself propagates instead, so the unit is abandoned without recording. When
    /// <paramref name="unit"/> is given, the dispatch shares the relay's scope and runs as a step of that unit; a message
    /// dispatched in a scope of its own passes <see langword="null"/>.
    /// </summary>
    private async Task<string?> DispatchAsync(IServiceProvider services, OutboxMessageData message, IOutboxRelayUnit? unit, CancellationToken cancellationToken)
    {
        Activity? activity = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(message.TraceParent))
            {
                activity = StartPublishingActivity(message);
            }

            var dispatcher = ResolveDispatcher(services, message.Destination);
            if (unit is null)
            {
                await dispatcher.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await unit.RunStepAsync(token => dispatcher.DispatchAsync(message, token), cancellationToken).ConfigureAwait(false);
            }

            Telemetry.Relayed.Add(1);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to publish outbox message {MessageId}", message.Id);
            Telemetry.Failed.Add(1);
            return exception.ToString();
        }
        finally
        {
            activity?.Dispose();
        }
    }

    /// <summary>
    /// Starts the relay span of a message, parented on the trace that captured it. A stored trace parent that is not a
    /// W3C trace context — an old hierarchical activity ID, or a value that other code wrote — starts a new trace
    /// instead, so trace data never fails a delivery.
    /// </summary>
    private Activity? StartPublishingActivity(OutboxMessageData message)
    {
        if (ActivityContext.TryParse(message.TraceParent, message.TraceState, out var parent))
        {
            return Telemetry.ActivitySource.StartActivity("OutboxPublishing", ActivityKind.Producer, parent);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("The trace parent of outbox message {MessageId} is not a W3C trace context; its relay span starts a new trace", message.Id);
        }

        return Telemetry.ActivitySource.StartActivity("OutboxPublishing", ActivityKind.Producer);
    }

    private static IOutboxDispatcher ResolveDispatcher(IServiceProvider services, OutboxDestination destination) =>
        services.GetServices<IOutboxDispatcher>().FirstOrDefault(dispatcher => dispatcher.Handles(destination))
            ?? throw new InvalidOperationException($"No {nameof(IOutboxDispatcher)} is registered for outbox destination '{destination}'.");
}

/// <summary>
/// The outcome of one relay cycle.
/// </summary>
/// <param name="Claimed">The number of messages the cycle claimed.</param>
/// <param name="Relayed">The number of claimed messages that were delivered.</param>
internal readonly record struct OutboxRelayCycleResult(int Claimed, int Relayed);
