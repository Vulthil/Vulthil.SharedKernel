using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Transport-agnostic registry that turns <see cref="QueueDefinition"/> registrations into per-message-type
/// <see cref="MessageExecutionPlan{THandler}"/> instances. It keys plans by wire URN (and CLR full name for the
/// bare-JSON receive path), fans a polymorphic registration out across every matching concrete subscription,
/// dedupes handlers, rejects a second request consumer for the same message type on the same queue, and attaches
/// the partition specification read from <see cref="IMessageConfigurationProvider.GetPartition"/>. A transport
/// drives it by supplying an <see cref="IMessageHandlerFactory{THandler}"/> that builds its own delivery closures.
/// </summary>
/// <typeparam name="THandler">The transport-specific handler type produced by the factory and stored in plans.</typeparam>
public sealed class MessageExecutionRegistry<THandler>
    where THandler : notnull
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly IMessageHandlerFactory<THandler> _handlerFactory;
    private readonly Dictionary<Uri, MessageExecutionPlan<THandler>> _plansByUrn = [];
    private readonly Dictionary<string, MessageExecutionPlan<THandler>> _plansByFullName = new(StringComparer.Ordinal);
    private readonly HashSet<(string QueueName, Uri Urn)> _requestConsumerKeys = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageExecutionRegistry{THandler}"/> class.
    /// </summary>
    /// <param name="provider">The resolved messaging configuration, used for URN and partition lookups.</param>
    /// <param name="handlerFactory">The transport factory that builds handlers for each registration.</param>
    public MessageExecutionRegistry(IMessageConfigurationProvider provider, IMessageHandlerFactory<THandler> handlerFactory)
    {
        _provider = provider;
        _handlerFactory = handlerFactory;
    }

    /// <summary>
    /// Gets the execution plans assembled so far, one per concrete message type, in no particular order.
    /// </summary>
    public IReadOnlyCollection<MessageExecutionPlan<THandler>> Plans => _plansByUrn.Values;

    /// <summary>
    /// Assembles execution plans for every concrete message type consumed or subscribed by <paramref name="queue"/>.
    /// Plans are keyed by URN within this registry instance, so registering several queues that consume the same
    /// message type accumulates all their handlers in one plan. A transport that delivers per queue must therefore
    /// build one registry per queue so a queue's deliveries dispatch only its own handlers (as the RabbitMQ
    /// transport does); a transport that dispatches each produced message exactly once can register every queue in
    /// a single instance (as the in-memory test harness does). A message type can have at most one request
    /// consumer per queue. Each handler is built with the registration's effective retry policy — the
    /// per-registration policy when set, otherwise the queue's <see cref="QueueDefinition.DefaultRetryPolicy"/> —
    /// which also applies to polymorphic registrations fanned out across their concrete implementers; request
    /// consumers never receive one, since they reply with an RPC fault instead of retrying.
    /// </summary>
    /// <param name="queue">The queue whose registrations and subscriptions to register.</param>
    /// <exception cref="InvalidOperationException">A second request consumer is registered for a message type that already has one on the same queue.</exception>
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

                var entry = registration is RequestConsumerRegistration rpc
                    ? _handlerFactory.ForRequestConsumer(rpc.ConsumerType.Type, rpc.MessageType.Type, rpc.ResponseType, retryPolicy: null)
                    : _handlerFactory.ForConsumer(registration.ConsumerType.Type, registration.MessageType.Type, registration.RetryPolicy ?? queue.DefaultRetryPolicy);

                if (entry.Kind == HandlerKind.RequestConsumer && !_requestConsumerKeys.Add((queue.Name, plan.Urn)))
                {
                    throw new InvalidOperationException(
                        $"Queue '{queue.Name}' already has a request consumer registered for message type '{subscription.Name}'. " +
                        "A message type can have at most one request consumer per queue, since multiple responses would be ambiguous.");
                }

                plan.Handlers.Add(entry.Handler);
            }
        }
    }

    private MessageExecutionPlan<THandler> GetOrAddPlan(MessageType messageType)
    {
        var urn = _provider.GetUrn(messageType.Type);
        if (_plansByUrn.TryGetValue(urn, out var existing))
        {
            return existing;
        }

        var plan = new MessageExecutionPlan<THandler>(messageType, urn)
        {
            Partition = _provider.GetPartition(messageType.Type),
        };
        _plansByUrn[urn] = plan;
        _plansByFullName[messageType.Name] = plan;
        return plan;
    }

    /// <summary>
    /// Indicates whether any concrete message type subscribed or consumed by <paramref name="queue"/> is
    /// partitioned, read directly from <see cref="IMessageConfigurationProvider.GetPartition"/>. Because it does
    /// not depend on built plans it is valid both during topology setup (which precedes <see cref="RegisterQueue"/>)
    /// and afterwards. Drives ordered single dispatch and the single-active-consumer queue argument.
    /// </summary>
    /// <param name="queue">The queue to inspect.</param>
    /// <returns><see langword="true"/> when any concrete message type on the queue is partitioned; otherwise <see langword="false"/>.</returns>
    public bool IsQueuePartitioned(QueueDefinition queue)
        => queue.Subscriptions.Any(s => _provider.GetPartition(s.MessageType.Type) is not null)
            || queue.Registrations.Any(r =>
                r.MessageType.Type is { IsAbstract: false, IsInterface: false }
                && _provider.GetPartition(r.MessageType.Type) is not null);

    /// <summary>
    /// Resolves a plan from the wire URN (envelope path). Returns <see langword="null"/> when no plan matches.
    /// </summary>
    /// <param name="urn">The wire URN as it appeared on the envelope.</param>
    /// <returns>The matching plan, or <see langword="null"/>.</returns>
    public MessageExecutionPlan<THandler>? GetPlanByUrn(Uri urn) => _plansByUrn.GetValueOrDefault(urn);

    /// <summary>
    /// Resolves a plan from the CLR full type name (bare-JSON compatibility path). Returns <see langword="null"/> when no plan matches.
    /// </summary>
    /// <param name="fullName">The CLR full type name.</param>
    /// <returns>The matching plan, or <see langword="null"/>.</returns>
    public MessageExecutionPlan<THandler>? GetPlanByFullName(string fullName) => _plansByFullName.GetValueOrDefault(fullName);

    /// <summary>
    /// Convenience lookup used by the bare-JSON receive path and tests: tries URN parsing first, then falls back to CLR full name.
    /// </summary>
    /// <param name="key">A wire URN or CLR full type name.</param>
    /// <returns>The matching plan, or <see langword="null"/>.</returns>
    public MessageExecutionPlan<THandler>? GetPlan(string key)
    {
        if (Uri.TryCreate(key, UriKind.Absolute, out var urn) && _plansByUrn.TryGetValue(urn, out var plan))
        {
            return plan;
        }
        return _plansByFullName.GetValueOrDefault(key);
    }
}
