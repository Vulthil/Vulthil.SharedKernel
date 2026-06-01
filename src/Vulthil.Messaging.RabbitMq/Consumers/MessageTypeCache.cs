using System.Collections.Concurrent;
using System.Reflection;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed class MessageTypeCache
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly Dictionary<Uri, MessageExecutionPlan> _plansByUrn = [];
    private readonly Dictionary<string, MessageExecutionPlan> _plansByFullName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(Type Consumer, Type Message), Func<RetryPolicyDefinition?, MessageHandler>> _consumerFactoryCache = new();
    private readonly ConcurrentDictionary<(Type Consumer, Type Request, Type Response), Func<RetryPolicyDefinition?, MessageHandler>> _requestConsumerFactoryCache = new();

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
        var effectiveSubscriptions = new HashSet<MessageType>(queue.Subscriptions.Select(s => s.MessageType));
        var concreteRegistrationTypes = queue.Registrations
            .Select(r => r.MessageType)
            .Where(m => m.Type is { IsAbstract: false, IsInterface: false });
        foreach (var concrete in concreteRegistrationTypes)
        {
            effectiveSubscriptions.Add(concrete);
        }

        foreach (var subscription in effectiveSubscriptions)
        {
            var concreteType = subscription.Type;
            var plan = GetOrAddPlan(subscription);

            foreach (var registration in queue.Registrations)
            {
                if (!registration.MessageType.Type.IsAssignableFrom(concreteType))
                {
                    continue;
                }

                if (registration is RequestConsumerRegistration rpc)
                {
                    if (plan.Handlers.Any(h => h.Kind == HandlerKind.RequestConsumer))
                    {
                        throw new InvalidOperationException(
                            $"Queue '{queue.Name}' already has a request consumer registered for message type '{subscription.Name}'. " +
                            "A message type can have at most one request consumer per queue, since multiple responses would be ambiguous.");
                    }

                    var rpcFactory = GetRequestConsumerFactory(rpc.ConsumerType.Type, rpc.MessageType.Type, rpc.ResponseType);
                    plan.Handlers.Add(rpcFactory(rpc.RetryPolicy));
                }
                else
                {
                    var consumerFactory = GetConsumerFactory(registration.ConsumerType.Type, registration.MessageType.Type);
                    plan.Handlers.Add(consumerFactory(registration.RetryPolicy));
                }
            }
        }
    }

    private Func<RetryPolicyDefinition?, MessageHandler> GetConsumerFactory(Type consumerType, Type messageType)
        => _consumerFactoryCache.GetOrAdd((consumerType, messageType), static key =>
            _forConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Message)
                .CreateDelegate<Func<RetryPolicyDefinition?, MessageHandler>>());

    private Func<RetryPolicyDefinition?, MessageHandler> GetRequestConsumerFactory(Type consumerType, Type requestType, Type responseType)
        => _requestConsumerFactoryCache.GetOrAdd((consumerType, requestType, responseType), static key =>
            _forRequestConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Request, key.Response)
                .CreateDelegate<Func<RetryPolicyDefinition?, MessageHandler>>());

    private MessageExecutionPlan GetOrAddPlan(MessageType messageType)
    {
        var urn = _provider.GetUrn(messageType.Type);
        if (_plansByUrn.TryGetValue(urn, out var existing))
        {
            return existing;
        }

        var partition = _provider.GetPartition(messageType.Type);
        var plan = new MessageExecutionPlan(messageType, urn)
        {
            Partitioner = partition?.Partitioner,
            PartitionKeyExtractor = partition is null
                ? null
                : PartitionKeyExtractorFactory.Build(messageType.Type, partition.KeySelector),
        };
        _plansByUrn[urn] = plan;
        _plansByFullName[messageType.Name] = plan;
        return plan;
    }

    /// <summary>
    /// Indicates whether any concrete message type subscribed or consumed by <paramref name="queue"/> is
    /// partitioned, read directly from <see cref="IMessageConfigurationProvider.GetPartition"/>. Because it
    /// does not depend on built plans it is valid both during topology setup (which precedes
    /// <see cref="RegisterQueue"/>) and afterwards. Drives ordered single dispatch and the single-active-consumer
    /// queue argument.
    /// </summary>
    public bool IsQueuePartitioned(QueueDefinition queue)
        => queue.Subscriptions.Any(s => _provider.GetPartition(s.MessageType.Type) is not null)
            || queue.Registrations.Any(r =>
                r.MessageType.Type is { IsAbstract: false, IsInterface: false }
                && _provider.GetPartition(r.MessageType.Type) is not null);

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
