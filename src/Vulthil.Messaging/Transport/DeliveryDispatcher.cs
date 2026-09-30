using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Runs one delivered message through its handlers with the delivery rules every transport shares:
/// <list type="bullet">
/// <item><description>Handlers run in rounds, one after another in plan order. The first round runs every handler it is
/// given; each later round runs only the handlers that failed in the round before, so a handler that completed never
/// runs again.</description></item>
/// <item><description>Every attempt runs in its own service scope, so one handler's scoped state never reaches
/// another's.</description></item>
/// <item><description>A failed handler retries under its own policy until its budget runs out or it throws an exception
/// its policy ignores. Then it has failed for good, and its fault is published at once.</description></item>
/// <item><description>Handlers retrying in the same round share one wait: the longest back-off any of their policies asks
/// for. The wait runs in-process, unless the port can redeliver later and no retrying policy is in-memory; then the
/// delivery goes back to the broker (see <see cref="DeliverySettlement.RedeliverLater"/>).</description></item>
/// <item><description>A request consumer runs once and answers with its response or an <see cref="RpcFault"/>; it never
/// retries and never publishes a fault.</description></item>
/// <item><description>When <see cref="IDeliveryPort.CancellationToken"/> ends the delivery, the dispatcher stops without a
/// fault or a reply for the interrupted attempt (see <see cref="DeliverySettlement.Abandon"/>).</description></item>
/// </list>
/// </summary>
public sealed partial class DeliveryDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryDispatcher"/> class.
    /// </summary>
    /// <param name="scopeFactory">Creates the service scope of each handler attempt.</param>
    /// <param name="logger">Logs handler failures and scheduled retries.</param>
    public DeliveryDispatcher(IServiceScopeFactory scopeFactory, ILogger<DeliveryDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs <paramref name="message"/> through <paramref name="handlers"/> and returns how the transport must settle
    /// the delivery.
    /// </summary>
    /// <param name="handlers">The handlers to run: a plan's handlers, or the subset a redelivery names.</param>
    /// <param name="message">The deserialized message.</param>
    /// <param name="port">The transport's port for this delivery.</param>
    /// <returns>The settlement, and what happened on the way there.</returns>
    public async Task<DeliveryOutcome> DispatchAsync(IReadOnlyCollection<DeliveryHandler> handlers, object message, IDeliveryPort port)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(port);

        var progress = new DeliveryProgress();
        var pending = handlers.ToList();
        var round = port.RetryCount;

        while (pending.Count > 0)
        {
            var failures = await RunRoundAsync(pending, message, port, round, progress).ConfigureAwait(false);
            if (failures is null)
            {
                return progress.Finish(DeliverySettlement.Abandon);
            }

            if (failures.Count == 0)
            {
                return progress.Finish(DeliverySettlement.Acknowledge);
            }

            var retrying = await SettleFailuresForGoodAsync(failures, port, round).ConfigureAwait(false);
            if (retrying.Count == 0)
            {
                return progress.Finish(DeliverySettlement.DeadLetter);
            }

            var delay = ScheduleRetry(retrying, round);
            if (port.CanRedeliverLater && retrying.TrueForAll(static failure => !failure.Handler.RetryPolicy!.InMemory))
            {
                return progress.FinishWithRedelivery(retrying.ConvertAll(static failure => failure.Handler.Identity), round + 1, delay);
            }

            try
            {
                await port.WaitBeforeRetryAsync(delay).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (port.CancellationToken.IsCancellationRequested)
            {
                return progress.Finish(DeliverySettlement.Abandon);
            }

            pending = retrying.ConvertAll(static failure => failure.Handler);
            round++;
        }

        return progress.Finish(DeliverySettlement.Acknowledge);
    }

    /// <summary>
    /// Runs every pending handler once. Returns the failures of the round, or <see langword="null"/> when the delivery
    /// was ended by <see cref="IDeliveryPort.CancellationToken"/>.
    /// </summary>
    private async Task<List<HandlerFailure>?> RunRoundAsync(List<DeliveryHandler> pending, object message, IDeliveryPort port, int round, DeliveryProgress progress)
    {
        var failures = new List<HandlerFailure>();
        foreach (var handler in pending)
        {
            DeliveryAttempt attempt;
            try
            {
                attempt = await RunAttemptAsync(handler, message, port, round).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (port.CancellationToken.IsCancellationRequested)
            {
                return null;
            }

            switch (attempt.Result)
            {
                case DeliveryAttemptResult.Consumed:
                    progress.Consumed.Add(handler);
                    break;
                case DeliveryAttemptResult.Failed:
                    progress.Failures.Add(attempt.Exception!);
                    failures.Add(new HandlerFailure(handler, attempt));
                    break;
            }
        }

        return failures;
    }

    private async Task<DeliveryAttempt> RunAttemptAsync(DeliveryHandler handler, object message, IDeliveryPort port, int round)
    {
        var scope = _scopeFactory.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);
        return await handler.Invoke(scope.ServiceProvider, message, port, round).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes the fault of every handler whose failure is final and returns the handlers that retry. A failure is
    /// final when the handler has no policy, its budget is spent, or its policy ignores the exception.
    /// </summary>
    private async Task<List<HandlerFailure>> SettleFailuresForGoodAsync(List<HandlerFailure> failures, IDeliveryPort port, int round)
    {
        var retrying = new List<HandlerFailure>();
        foreach (var failure in failures)
        {
            var exception = failure.Attempt.Exception!;
            if (failure.Handler.RetryPolicy is { } policy
                && round < policy.MaxRetryCount
                && !policy.GetIgnoredExceptionTypes().Contains(exception.GetType()))
            {
                retrying.Add(failure);
                continue;
            }

            LogConsumerFailed(_logger, exception, failure.Handler.Identity, round);
            await failure.Attempt.PublishFault!(port).ConfigureAwait(false);
        }

        return retrying;
    }

    /// <summary>
    /// Logs each retrying failure and returns the round's wait: the longest back-off any retrying handler's policy
    /// asks for, so no handler retries earlier than its own policy allows.
    /// </summary>
    private TimeSpan ScheduleRetry(List<HandlerFailure> retrying, int round)
    {
        var delay = TimeSpan.Zero;
        var maxRetryCount = 0;
        foreach (var failure in retrying)
        {
            var policy = failure.Handler.RetryPolicy!;
            LogConsumerThrew(_logger, failure.Attempt.Exception!, failure.Handler.Identity, round, policy.MaxRetryCount);

            var handlerDelay = policy.GetDelay(round);
            delay = handlerDelay > delay ? handlerDelay : delay;
            maxRetryCount = Math.Max(maxRetryCount, policy.MaxRetryCount);
        }

        LogSchedulingRetry(_logger, round + 1, maxRetryCount, delay);
        return delay;
    }

    [LoggerMessage(EventId = 2200, Level = LogLevel.Warning,
        Message = "Consumer {Consumer} threw on retry round {Retry} of up to {MaxRetry}; it will retry")]
    private static partial void LogConsumerThrew(ILogger logger, Exception exception, string consumer, int retry, int maxRetry);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Debug,
        Message = "Scheduling retry round {Retry} of up to {MaxRetry} after {Delay}")]
    private static partial void LogSchedulingRetry(ILogger logger, int retry, int maxRetry, TimeSpan delay);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Error,
        Message = "Consumer {Consumer} failed for good on retry round {Retry}; publishing its fault")]
    private static partial void LogConsumerFailed(ILogger logger, Exception exception, string consumer, int retry);

    private sealed record HandlerFailure(DeliveryHandler Handler, DeliveryAttempt Attempt);

    private sealed class DeliveryProgress
    {
        public List<DeliveryHandler> Consumed { get; } = [];

        public List<Exception> Failures { get; } = [];

        public DeliveryOutcome Finish(DeliverySettlement settlement)
            => new(settlement, Consumed, Failures, [], 0, TimeSpan.Zero);

        public DeliveryOutcome FinishWithRedelivery(List<string> handlerIdentities, int retryCount, TimeSpan delay)
            => new(DeliverySettlement.RedeliverLater, Consumed, Failures, handlerIdentities, retryCount, delay);
    }
}
