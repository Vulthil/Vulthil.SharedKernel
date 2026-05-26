using System.Collections.Concurrent;
using System.Reflection;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed class MessageTypeCache
{
    private readonly Dictionary<string, MessageExecutionPlan> _plans = [];

    // Process-wide caches keyed by closed-generic type parameters. Values are pure delegates with no
    // per-cache state, so concurrent registration across tests/queues is safe.
    private static readonly ConcurrentDictionary<(Type Consumer, Type Message), Func<string, RetryPolicyDefinition?, MessageHandler>> _consumerFactoryCache = new();
    private static readonly ConcurrentDictionary<(Type Consumer, Type Request, Type Response), Func<string, RetryPolicyDefinition?, MessageHandler>> _requestConsumerFactoryCache = new();

    private static readonly MethodInfo _forConsumerMethod = typeof(MessageHandlerFactory)
        .GetMethod(nameof(MessageHandlerFactory.ForConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(MessageHandlerFactory)}.{nameof(MessageHandlerFactory.ForConsumer)} not found.");

    private static readonly MethodInfo _forRequestConsumerMethod = typeof(MessageHandlerFactory)
        .GetMethod(nameof(MessageHandlerFactory.ForRequestConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(MessageHandlerFactory)}.{nameof(MessageHandlerFactory.ForRequestConsumer)} not found.");

    public void RegisterQueue(QueueDefinition queue)
    {
        foreach (var consumer in queue.Registrations.OfType<ConsumerRegistration>())
        {
            var msgType = consumer.MessageType;
            var plan = GetOrAddPlan(msgType.Name, msgType);

            var factory = GetConsumerFactory(consumer.ConsumerType.Type, msgType.Type);
            var handler = factory(RabbitMqConstants.GetRoutingKey(consumer), consumer.RetryPolicy);
            plan.Handlers.Add(handler);
        }

        foreach (var rpc in queue.Registrations.OfType<RequestConsumerRegistration>())
        {
            var msgType = rpc.MessageType;
            var plan = GetOrAddPlan(msgType.Name, msgType);

            if (plan.Handlers.Any(h => h.Kind == HandlerKind.RequestConsumer))
            {
                throw new InvalidOperationException(
                    $"Queue '{queue.Name}' already has a request consumer registered for message type '{msgType.Name}'. " +
                    "A message type can have at most one request consumer per queue, since multiple responses would be ambiguous.");
            }

            var factory = GetRequestConsumerFactory(rpc.ConsumerType.Type, msgType.Type, rpc.ResponseType);
            var handler = factory(RabbitMqConstants.GetRoutingKey(rpc), rpc.RetryPolicy);
            plan.Handlers.Add(handler);
        }
    }

    private static Func<string, RetryPolicyDefinition?, MessageHandler> GetConsumerFactory(Type consumerType, Type messageType)
        => _consumerFactoryCache.GetOrAdd((consumerType, messageType), static key =>
            _forConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Message)
                .CreateDelegate<Func<string, RetryPolicyDefinition?, MessageHandler>>());

    private static Func<string, RetryPolicyDefinition?, MessageHandler> GetRequestConsumerFactory(Type consumerType, Type requestType, Type responseType)
        => _requestConsumerFactoryCache.GetOrAdd((consumerType, requestType, responseType), static key =>
            _forRequestConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Request, key.Response)
                .CreateDelegate<Func<string, RetryPolicyDefinition?, MessageHandler>>());

    private MessageExecutionPlan GetOrAddPlan(string name, MessageType type)
    {
        if (!_plans.TryGetValue(name, out var plan))
        {
            plan = new MessageExecutionPlan(type);
            _plans[name] = plan;
        }
        return plan;
    }

    public MessageExecutionPlan? GetPlan(string key) => _plans.GetValueOrDefault(key);
}
