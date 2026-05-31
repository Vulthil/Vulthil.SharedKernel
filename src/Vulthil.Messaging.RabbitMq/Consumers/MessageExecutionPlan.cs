using RabbitMQ.Client.Events;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Envelope;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed record MessageExecutionPlan(MessageType MessageType, Uri Urn)
{
    /// <summary>
    /// The set of handlers that should run when a message of <see cref="MessageType"/> is delivered.
    /// The broker is authoritative for delivery (queue-binding filter); every handler in this list runs on every delivery.
    /// </summary>
    public List<MessageHandler> Handlers { get; } = [];

    /// <summary>
    /// The partitioner whose lanes serialize same-key deliveries of this message type, or
    /// <see langword="null"/> when the type is not partitioned.
    /// </summary>
    public Partitioner? Partitioner { get; init; }

    /// <summary>
    /// Extracts the partition key from a delivered message (and its transport metadata), or
    /// <see langword="null"/> when the type is not partitioned.
    /// </summary>
    public Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?>? PartitionKeyExtractor { get; init; }

    /// <summary>Gets a value indicating whether deliveries of this message type are partitioned for ordered processing.</summary>
    public bool IsPartitioned => Partitioner is not null && PartitionKeyExtractor is not null;
}
