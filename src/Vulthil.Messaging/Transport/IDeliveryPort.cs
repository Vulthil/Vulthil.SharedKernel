using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// What a transport provides for one delivered message so a <see cref="DeliveryDispatcher"/> can run it: the receive
/// context, the in-process retry wait, and the routes for faults and replies. The dispatcher decides everything else —
/// which handlers run in which round, each handler's retries, and when a handler has failed for good — and returns the
/// settlement for the transport to apply (see <see cref="DeliveryOutcome"/>). Create one port per delivery.
/// </summary>
public interface IDeliveryPort
{
    /// <summary>
    /// Gets the retry round this delivery starts at: 0 for a first delivery, or the round a redelivery carries.
    /// </summary>
    int RetryCount { get; }

    /// <summary>
    /// Gets the token that ends the delivery, for example when the transport shuts down. When it is cancelled and a
    /// handler throws <see cref="OperationCanceledException"/>, the dispatcher stops and returns
    /// <see cref="DeliverySettlement.Abandon"/>.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets whether the transport can hand a failed delivery back to the broker for a delayed redelivery (for example
    /// a retry queue with a per-message TTL). When <see langword="false"/>, every retry runs in-process after
    /// <see cref="WaitBeforeRetryAsync"/>.
    /// </summary>
    bool CanRedeliverLater { get; }

    /// <summary>
    /// Builds the receive context for one attempt at <paramref name="message"/>. Must not throw.
    /// </summary>
    /// <typeparam name="TMessage">The message type the handler consumes.</typeparam>
    /// <param name="message">The delivered message.</param>
    /// <param name="services">The attempt's own service scope.</param>
    /// <param name="retryCount">The retry round of this attempt, for <see cref="IMessageContext.RetryCount"/>.</param>
    /// <returns>The receive context the consume pipeline runs with.</returns>
    MessageContext<TMessage> CreateContext<TMessage>(TMessage message, IServiceProvider services, int retryCount)
        where TMessage : notnull;

    /// <summary>
    /// Waits <paramref name="delay"/> before an in-process retry round.
    /// </summary>
    /// <param name="delay">The longest back-off any retrying handler's policy asks for.</param>
    /// <returns>A task that completes when the round may start.</returns>
    /// <exception cref="OperationCanceledException"><see cref="CancellationToken"/> ended the delivery first.</exception>
    Task WaitBeforeRetryAsync(TimeSpan delay);

    /// <summary>
    /// Publishes the fault of a consumer that failed for good. Publishing is best-effort: this must not throw, because
    /// a failed fault publish never stops the delivery from being settled.
    /// </summary>
    /// <typeparam name="TMessage">The faulted message type.</typeparam>
    /// <param name="fault">The fault, carrying the message, the exception and the attempt's context.</param>
    /// <returns>A task that completes when the fault is handed off.</returns>
    Task PublishFaultAsync<TMessage>(Fault<TMessage> fault)
        where TMessage : notnull;

    /// <summary>
    /// Sends a request consumer's reply — the response, or an <see cref="RpcFault"/> — to the requester.
    /// </summary>
    /// <param name="reply">The reply, built with <see cref="RpcReply"/>.</param>
    /// <returns>A task that completes when the reply is sent.</returns>
    Task SendReplyAsync(MessageEnvelope reply);
}
