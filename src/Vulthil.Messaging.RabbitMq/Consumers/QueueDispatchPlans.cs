using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// The dispatch plans of one queue, keyed by wire URN: the RabbitMQ adapter over
/// <see cref="MessageExecutionRegistry{THandler}"/>. Several queues may consume the same message type and the
/// broker delivers a distinct copy to each, so a delivery must dispatch only the handlers its own queue registered;
/// building the registry for exactly one queue here makes that scoping the only possible shape. Every plan is
/// decorated with its AMQP partition at construction, so delivery-time lookups stay read-only.
/// </summary>
internal sealed class QueueDispatchPlans
{
    private readonly MessageExecutionRegistry<MessageHandler> _registry;
    private readonly Dictionary<Uri, RabbitMqPlan> _plansByUrn;

    /// <summary>
    /// Assembles the plans for every concrete message type <paramref name="queue"/> consumes or subscribes to.
    /// </summary>
    /// <param name="provider">The resolved messaging configuration, used for URN and partition lookups.</param>
    /// <param name="queue">The queue whose registrations and subscriptions the plans dispatch for.</param>
    /// <exception cref="InvalidOperationException"><paramref name="queue"/> registers a second request consumer for a message type that already has one.</exception>
    public QueueDispatchPlans(IMessageConfigurationProvider provider, QueueDefinition queue)
    {
        _registry = new MessageExecutionRegistry<MessageHandler>(provider, new RabbitMqHandlerFactory());
        _registry.RegisterQueue(queue);
        IsPartitioned = _registry.IsQueuePartitioned(queue);
        _plansByUrn = _registry.Plans.ToDictionary(plan => plan.Urn, BuildPlan);
    }

    /// <summary>
    /// Gets a value indicating whether any concrete message type the queue consumes or subscribes to is partitioned.
    /// A partitioned queue consumes from a single channel with ordered dispatch and declares itself with the
    /// broker's single-active-consumer argument.
    /// </summary>
    public bool IsPartitioned { get; }

    /// <summary>Resolves the plan for a wire URN (envelope path), or <see langword="null"/> when the queue has none for it.</summary>
    public RabbitMqPlan? GetPlanByUrn(Uri urn) => _plansByUrn.GetValueOrDefault(urn);

    /// <summary>
    /// Resolves a plan from a wire URN or a CLR full type name (bare-JSON receive path), or <see langword="null"/>
    /// when the queue has none for it.
    /// </summary>
    public RabbitMqPlan? GetPlan(string key)
    {
        var core = _registry.GetPlan(key);
        return core is null ? null : _plansByUrn[core.Urn];
    }

    private static RabbitMqPlan BuildPlan(MessageExecutionPlan<MessageHandler> plan)
    {
        var partition = plan.Partition is null
            ? null
            : new RabbitMqPartition(plan.Partition.Partitioner, PartitionKeyExtractorFactory.Build(plan.MessageType.Type, plan.Partition.KeySelector));
        return new RabbitMqPlan(plan.MessageType, plan.Handlers, partition);
    }
}
