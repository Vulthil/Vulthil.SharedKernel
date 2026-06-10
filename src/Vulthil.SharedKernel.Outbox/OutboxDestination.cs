namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Identifies the sink an outbox message is relayed to, so the <see cref="OutboxProcessor"/> can route each row to
/// the matching <see cref="IOutboxDispatcher"/>. One outbox table can carry messages bound for different sinks.
/// </summary>
public enum OutboxDestination
{
    /// <summary>An in-process domain event, dispatched to <c>IDomainEventHandler</c> implementations.</summary>
    DomainEvent = 0,

    /// <summary>A message published to the broker (pub/sub).</summary>
    Publish = 1,

    /// <summary>A message sent point-to-point to a broker endpoint.</summary>
    Send = 2,
}
