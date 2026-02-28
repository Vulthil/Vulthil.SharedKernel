using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed record MessageExecutionPlan(MessageType MessageType)
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public List<IConsumerInvoker> StandardHandlers { get; } = [];
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public IRpcInvoker? RpcHandler { get; set; }
}

internal sealed class MessageTypeCache
{
    private readonly Dictionary<string, MessageExecutionPlan> _plans = [];
    /// <summary>
    /// Executes this member.
    /// </summary>
    public void RegisterQueue(QueueDefinition queue, MessagingOptions messagingOptions)
    {
        foreach (var consumer in queue.Registrations.OfType<ConsumerRegistration>())
        {
            var msgType = consumer.MessageType;
            var plan = GetOrAddPlan(msgType.Name, msgType);

            var invokerType = typeof(ConsumerInvoker<,>)
                .MakeGenericType(consumer.ConsumerType.Type, msgType.Type);

            var invoker = (IConsumerInvoker)Activator.CreateInstance(
                invokerType,
                args: [RabbitMqConstants.GetRoutingKey(consumer), consumer.RetryPolicy]
                )!;
            plan.StandardHandlers.Add(invoker);
        }

        foreach (var rpc in queue.Registrations.OfType<RequestConsumerRegistration>())
        {
            var msgType = rpc.MessageType;
            var plan = GetOrAddPlan(msgType.Name, msgType);

            var invokerType = typeof(RpcInvoker<,,>)
                .MakeGenericType(rpc.ConsumerType.Type, msgType.Type, rpc.ResponseType);

            var invoker = (IRpcInvoker)Activator.CreateInstance(
                     invokerType,
                     args: [messagingOptions, RabbitMqConstants.GetRoutingKey(rpc), rpc.RetryPolicy]
            )!;
            plan.RpcHandler = invoker;
        }
    }

    private MessageExecutionPlan GetOrAddPlan(string name, MessageType type)
    {
        if (!_plans.TryGetValue(name, out var plan))
        {
            plan = new MessageExecutionPlan(type);
            _plans[name] = plan;
        }
        return plan;
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    public MessageExecutionPlan? GetPlan(string key) => _plans.GetValueOrDefault(key);
}
