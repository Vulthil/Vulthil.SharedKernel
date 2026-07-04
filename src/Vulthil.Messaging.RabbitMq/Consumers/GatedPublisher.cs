using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Publishes a message on a consumer worker's shared channel, serialized through the worker's channel gate
/// alongside the delivery settles (acks, nacks, retry republishes and fault publishes) — RabbitMQ channels must
/// not be used concurrently. Handed to <see cref="MessageHandler.DispatchAsync"/> in place of the raw channel so
/// a request/reply handler's response publish cannot interleave frames with a parallel settle on the same channel.
/// </summary>
/// <param name="exchange">The exchange to publish to; an empty string targets the broker's default exchange.</param>
/// <param name="routingKey">The routing key (the destination queue name when using the default exchange).</param>
/// <param name="mandatory">Whether the broker should return the message when it cannot be routed.</param>
/// <param name="basicProperties">The AMQP properties to publish with.</param>
/// <param name="body">The serialized message body.</param>
/// <returns>A task that completes once the publish has been written to the channel.</returns>
internal delegate Task GatedPublisher(string exchange, string routingKey, bool mandatory, BasicProperties basicProperties, ReadOnlyMemory<byte> body);
