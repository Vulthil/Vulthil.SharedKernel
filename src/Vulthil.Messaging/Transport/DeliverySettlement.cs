namespace Vulthil.Messaging.Transport;

/// <summary>
/// How a transport must settle a delivery once a <see cref="DeliveryDispatcher"/> has run it.
/// </summary>
public enum DeliverySettlement
{
    /// <summary>
    /// Acknowledge the delivery: every handler in the final round completed. A handler that failed for good in an
    /// earlier round, while others kept retrying, has already published its fault.
    /// </summary>
    Acknowledge,

    /// <summary>
    /// Dead-letter the delivery: the final round ended with a handler that failed for good, and its fault is already
    /// published.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Hand the delivery back to the broker for a delayed redelivery that runs only the handlers in
    /// <see cref="DeliveryOutcome.RedeliveryHandlerIdentities"/>, starting at
    /// <see cref="DeliveryOutcome.RedeliveryRetryCount"/> after <see cref="DeliveryOutcome.RedeliveryDelay"/>; then
    /// acknowledge this delivery.
    /// </summary>
    RedeliverLater,

    /// <summary>
    /// Leave the delivery unsettled: <see cref="IDeliveryPort.CancellationToken"/> ended it before it finished, so the
    /// broker can deliver it again. No fault and no reply was sent for the interrupted attempt.
    /// </summary>
    Abandon,
}
