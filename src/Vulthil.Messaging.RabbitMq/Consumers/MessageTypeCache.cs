using System.Collections.Concurrent;
using System.Reflection;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed class MessageTypeCache
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly Dictionary<Uri, MessageExecutionPlan> _plansByUrn = [];
    private readonly Dictionary<string, MessageExecutionPlan> _plansByFullName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(Type Consumer, Type Message), Func<string, RetryPolicyDefinition?, MessageHandler>> _consumerFactoryCache = new();
    private readonly ConcurrentDictionary<(Type Consumer, Type Request, Type Response), Func<string, RetryPolicyDefinition?, MessageHandler>> _requestConsumerFactoryCache = new();

    private static readonly MethodInfo _forConsumerMethod = typeof(MessageHandlerFactory)
        .GetMethod(nameof(MessageHandlerFactory.ForConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(MessageHandlerFactory)}.{nameof(MessageHandlerFactory.ForConsumer)} not found.");
    private static readonly MethodInfo _forRequestConsumerMethod = typeof(MessageHandlerFactory)
        .GetMethod(nameof(MessageHandlerFactory.ForRequestConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(MessageHandlerFactory)}.{nameof(MessageHandlerFactory.ForRequestConsumer)} not found.");

    public MessageTypeCache(IMessageConfigurationProvider provider)
    {
        _provider = provider;
    }

    public void RegisterQueue(QueueDefinition queue)
    {
        foreach (var consumer in queue.Registrations.OfType<ConsumerRegistration>())
        {
            var msgType = consumer.MessageType;
            var plan = GetOrAddPlan(msgType);

            var factory = GetConsumerFactory(consumer.ConsumerType.Type, msgType.Type);
            var handler = factory(RabbitMqConstants.GetRoutingKey(consumer), consumer.RetryPolicy);
            plan.Handlers.Add(handler);
        }

        foreach (var rpc in queue.Registrations.OfType<RequestConsumerRegistration>())
        {
            var msgType = rpc.MessageType;
            var plan = GetOrAddPlan(msgType);

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

    private Func<string, RetryPolicyDefinition?, MessageHandler> GetConsumerFactory(Type consumerType, Type messageType)
        => _consumerFactoryCache.GetOrAdd((consumerType, messageType), static key =>
            _forConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Message)
                .CreateDelegate<Func<string, RetryPolicyDefinition?, MessageHandler>>());

    private Func<string, RetryPolicyDefinition?, MessageHandler> GetRequestConsumerFactory(Type consumerType, Type requestType, Type responseType)
        => _requestConsumerFactoryCache.GetOrAdd((consumerType, requestType, responseType), static key =>
            _forRequestConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Request, key.Response)
                .CreateDelegate<Func<string, RetryPolicyDefinition?, MessageHandler>>());

    private MessageExecutionPlan GetOrAddPlan(MessageType messageType)
    {
        var urn = _provider.GetUrn(messageType.Type);
        if (_plansByUrn.TryGetValue(urn, out var existing))
        {
            return existing;
        }

        var plan = new MessageExecutionPlan(messageType, urn);
        _plansByUrn[urn] = plan;
        _plansByFullName[messageType.Name] = plan;
        return plan;
    }

    /// <summary>
    /// Resolves a plan from the wire URN (envelope path). Returns <see langword="null"/> when no plan matches.
    /// </summary>
    public MessageExecutionPlan? GetPlanByUrn(Uri urn) => _plansByUrn.GetValueOrDefault(urn);

    /// <summary>
    /// Resolves a plan from the CLR full type name (bare-JSON compat path). Returns <see langword="null"/> when no plan matches.
    /// </summary>
    public MessageExecutionPlan? GetPlanByFullName(string fullName) => _plansByFullName.GetValueOrDefault(fullName);

    /// <summary>
    /// Convenience lookup used by tests and the bare-JSON receive path: tries URN parsing first, falls back to CLR full name.
    /// </summary>
    public MessageExecutionPlan? GetPlan(string key)
    {
        if (Uri.TryCreate(key, UriKind.Absolute, out var urn) && _plansByUrn.TryGetValue(urn, out var plan))
        {
            return plan;
        }
        return _plansByFullName.GetValueOrDefault(key);
    }
}
