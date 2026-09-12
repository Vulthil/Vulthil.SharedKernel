using RabbitMQ.Client.Events;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// How a partitioned message type is laned on the RabbitMQ transport: the <see cref="Partitioner"/> whose lanes
/// serialize same-key deliveries, and the extractor that reads the partition key from a deserialized message and
/// its AMQP delivery. A <see langword="null"/> or empty key means the delivery runs unlaned.
/// </summary>
/// <param name="Partitioner">The partitioner whose lanes serialize same-key deliveries.</param>
/// <param name="ExtractKey">Reads the partition key from the message, its delivery, and the envelope when the delivery carried one.</param>
internal sealed record RabbitMqPartition(Partitioner Partitioner, Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?> ExtractKey);
