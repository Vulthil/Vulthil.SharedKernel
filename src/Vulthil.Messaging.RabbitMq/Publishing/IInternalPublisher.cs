using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal interface IInternalPublisher
{
    Task InternalPublishAsync<TMessage>(
        byte[] body,
        BasicProperties props,
        string routingKey,
        MessageConfiguration messageConfiguration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a message to the broker's default exchange using the supplied queue name as the routing key.
    /// No topology declaration is performed — the destination queue is owned by the receiving service.
    /// </summary>
    Task InternalSendAsync(
        byte[] body,
        BasicProperties props,
        string queueName,
        CancellationToken cancellationToken);
}
