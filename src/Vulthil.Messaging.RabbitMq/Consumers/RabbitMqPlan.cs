using System.Collections.ObjectModel;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// RabbitMQ view over a core <see cref="MessageExecutionPlan{THandler}"/>: surfaces the handler list and the
/// resolved <see cref="Partitioner"/> plus the AMQP-aware partition key extractor the worker needs to lane
/// partitioned deliveries. Wraps the core plan by reference, so handlers added after construction are observed.
/// </summary>
internal sealed class RabbitMqPlan
{
    private readonly MessageExecutionPlan<MessageHandler> _core;

    public RabbitMqPlan(MessageExecutionPlan<MessageHandler> core, Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?>? partitionKeyExtractor)
    {
        _core = core;
        PartitionKeyExtractor = partitionKeyExtractor;
    }

    /// <summary>The concrete message type this plan dispatches.</summary>
    public MessageType MessageType => _core.MessageType;

    /// <summary>The handlers that run on every delivery of <see cref="MessageType"/>.</summary>
    public Collection<MessageHandler> Handlers => _core.Handlers;

    /// <summary>The partitioner whose lanes serialize same-key deliveries, or <see langword="null"/> when not partitioned.</summary>
    public Partitioner? Partitioner => _core.Partition?.Partitioner;

    /// <summary>
    /// Extracts the partition key from a delivered message (and its transport metadata), or
    /// <see langword="null"/> when the type is not partitioned.
    /// </summary>
    public Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?>? PartitionKeyExtractor { get; }

    /// <summary>Gets a value indicating whether deliveries of this message type are partitioned for ordered processing.</summary>
    public bool IsPartitioned => Partitioner is not null && PartitionKeyExtractor is not null;
}
