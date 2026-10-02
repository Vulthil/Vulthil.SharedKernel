using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal interface IInternalPublisher
{
    /// <summary>
    /// Publishes a built message over its message type's fanout/topic exchange, declaring the exchange first.
    /// </summary>
    Task InternalPublishAsync(
        RabbitMqOutgoingMessage message,
        MessageConfiguration messageConfiguration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a built send to the broker's default exchange, routed to its destination queue. No topology
    /// declaration is performed — the destination queue is owned by the receiving service.
    /// </summary>
    Task InternalSendAsync(
        RabbitMqOutgoingMessage message,
        CancellationToken cancellationToken);
}
