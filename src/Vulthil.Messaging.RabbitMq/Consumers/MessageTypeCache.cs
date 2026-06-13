using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// RabbitMQ adapter over <see cref="MessageExecutionRegistry{THandler}"/>. Delegates plan assembly to the core
/// registry and decorates each resolved plan with a <see cref="RabbitMqPlan"/> carrying the AMQP partition key
/// extractor. Wrappers are built once per plan (keyed by URN) during registration, so delivery-time lookups stay
/// read-only.
/// </summary>
internal sealed class MessageTypeCache
{
    private readonly MessageExecutionRegistry<MessageHandler> _registry;
    private readonly Dictionary<Uri, RabbitMqPlan> _wrappers = [];

    public MessageTypeCache(IMessageConfigurationProvider provider)
        => _registry = new MessageExecutionRegistry<MessageHandler>(provider, new RabbitMqHandlerFactory());

    public void RegisterQueue(QueueDefinition queue)
    {
        _registry.RegisterQueue(queue);
        foreach (var plan in _registry.Plans.Where(plan => !_wrappers.ContainsKey(plan.Urn)))
        {
            _wrappers[plan.Urn] = BuildWrapper(plan);
        }
    }

    /// <inheritdoc cref="MessageExecutionRegistry{THandler}.IsQueuePartitioned"/>
    public bool IsQueuePartitioned(QueueDefinition queue) => _registry.IsQueuePartitioned(queue);

    /// <summary>Resolves a wrapped plan from the wire URN (envelope path). Returns <see langword="null"/> when no plan matches.</summary>
    public RabbitMqPlan? GetPlanByUrn(Uri urn) => _wrappers.GetValueOrDefault(urn);

    /// <summary>Resolves a wrapped plan from the CLR full type name (bare-JSON compat path). Returns <see langword="null"/> when no plan matches.</summary>
    public RabbitMqPlan? GetPlanByFullName(string fullName)
    {
        var core = _registry.GetPlanByFullName(fullName);
        return core is null ? null : _wrappers.GetValueOrDefault(core.Urn);
    }

    /// <summary>Convenience lookup used by the bare-JSON receive path and tests: tries URN parsing first, falls back to CLR full name.</summary>
    public RabbitMqPlan? GetPlan(string key)
    {
        var core = _registry.GetPlan(key);
        return core is null ? null : _wrappers.GetValueOrDefault(core.Urn);
    }

    private static RabbitMqPlan BuildWrapper(MessageExecutionPlan<MessageHandler> plan)
    {
        var extractor = plan.Partition is null
            ? null
            : PartitionKeyExtractorFactory.Build(plan.MessageType.Type, plan.Partition.KeySelector);
        return new RabbitMqPlan(plan, extractor);
    }
}
