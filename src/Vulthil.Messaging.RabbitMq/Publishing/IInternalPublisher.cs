using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal interface IInternalPublisher
{
    Task InternalPublishAsync<TMessage>(
        byte[] body,
        BasicProperties props,
        string routingKey,
        CancellationToken cancellationToken);
}
