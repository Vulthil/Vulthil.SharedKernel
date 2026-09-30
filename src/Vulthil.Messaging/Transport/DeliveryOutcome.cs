namespace Vulthil.Messaging.Transport;

/// <summary>
/// How a <see cref="DeliveryDispatcher"/> finished a delivery: the settlement the transport must apply, and what
/// happened on the way there.
/// </summary>
public sealed class DeliveryOutcome
{
    internal DeliveryOutcome(
        DeliverySettlement settlement,
        IReadOnlyList<DeliveryHandler> consumed,
        IReadOnlyList<Exception> failures,
        IReadOnlyList<string> redeliveryHandlerIdentities,
        int redeliveryRetryCount,
        TimeSpan redeliveryDelay)
    {
        Settlement = settlement;
        Consumed = consumed;
        Failures = failures;
        RedeliveryHandlerIdentities = redeliveryHandlerIdentities;
        RedeliveryRetryCount = redeliveryRetryCount;
        RedeliveryDelay = redeliveryDelay;
    }

    /// <summary>Gets how the transport must settle the delivery.</summary>
    public DeliverySettlement Settlement { get; }

    /// <summary>
    /// Gets the handlers whose consumer completed, in the order they completed. A handler whose pipeline a filter
    /// completed without calling the consumer is not included, and neither is a request consumer that answered with an
    /// <see cref="RpcFault"/>.
    /// </summary>
    public IReadOnlyList<DeliveryHandler> Consumed { get; }

    /// <summary>Gets every exception a one-way consumer threw during the delivery, in the order they were thrown.</summary>
    public IReadOnlyList<Exception> Failures { get; }

    /// <summary>
    /// Gets, for <see cref="DeliverySettlement.RedeliverLater"/>, the identities of the handlers the redelivery must
    /// run (see <see cref="DeliveryHandler.Identity"/>); empty otherwise.
    /// </summary>
    public IReadOnlyList<string> RedeliveryHandlerIdentities { get; }

    /// <summary>
    /// Gets, for <see cref="DeliverySettlement.RedeliverLater"/>, the retry round the redelivery starts at (its
    /// <see cref="IDeliveryPort.RetryCount"/>); 0 otherwise.
    /// </summary>
    public int RedeliveryRetryCount { get; }

    /// <summary>
    /// Gets, for <see cref="DeliverySettlement.RedeliverLater"/>, how long the broker should hold the delivery before
    /// redelivering it: the longest back-off any retrying handler's policy asks for. <see cref="TimeSpan.Zero"/>
    /// otherwise.
    /// </summary>
    public TimeSpan RedeliveryDelay { get; }
}
