namespace Vulthil.Messaging.Queues;

/// <summary>
/// Wraps a CLR type representing a message exchanged through the messaging infrastructure.
/// </summary>
/// <param name="Type">The CLR type of the message.</param>
public sealed record MessageType(Type Type)
{
    /// <summary>
    /// Gets the fully-qualified CLR type name used as the message routing identifier.
    /// </summary>
    public string Name => Type.FullName!;
}
/// <summary>
/// Wraps a CLR type representing a consumer in the messaging infrastructure.
/// </summary>
/// <param name="Type">The CLR type of the consumer.</param>
public sealed record ConsumerType(Type Type)
{
    /// <summary>
    /// Gets the fully-qualified CLR type name used to identify the consumer.
    /// </summary>
    public string Name => Type.FullName!;
}

/// <summary>
/// Base record for a consumer registration on a queue, binding a consumer to the message type it handles.
/// Routing-key patterns belong on <see cref="Subscription"/> (queue→exchange binding) or
/// <see cref="MessageConfiguration{TMessage}"/> (producer-side routing key).
/// </summary>
public abstract record Registration
{
    /// <summary>
    /// Gets the consumer type responsible for processing the bound message.
    /// </summary>
    public required ConsumerType ConsumerType { get; init; }
    /// <summary>
    /// Gets the message type that this consumer is registered to handle.
    /// </summary>
    public required MessageType MessageType { get; init; }

    /// <summary>
    /// Gets the per-consumer retry policy, or <see langword="null"/> to inherit the queue-level default.
    /// Ignored for request-consumer registrations: request consumers do not retry — a thrown exception is
    /// returned to the requester as an RPC fault reply instead.
    /// </summary>
    public RetryPolicyDefinition? RetryPolicy { get; init; }
}

/// <summary>
/// A consumer registration for one-way message consumption.
/// </summary>
public sealed record ConsumerRegistration : Registration;

/// <summary>
/// Binds the queue to a concrete message type's exchange. One <see cref="Subscription"/> = one
/// <c>exchange→queue</c> binding declared at topology setup time.
/// </summary>
/// <param name="MessageType">The concrete message type whose exchange will be bound.</param>
/// <param name="RoutingKey">
/// The binding pattern used by the broker to filter deliveries. <see langword="null"/> = the broker
/// receives an empty pattern: fanout/headers exchanges ignore it, direct/topic exchanges only match
/// messages published with an empty (direct) or unmatchable (topic) routing key. Supply a specific
/// pattern (e.g. <c>"order.created"</c> for direct, <c>"order.*"</c> for topic) when needed.
/// </param>
public sealed record Subscription(MessageType MessageType, string? RoutingKey = null);

/// <summary>
/// A consumer registration for request/reply message consumption.
/// </summary>
public sealed record RequestConsumerRegistration : Registration
{
    /// <summary>
    /// Gets the CLR type of the response produced by the request consumer when handling the request.
    /// </summary>
    public required Type ResponseType { get; init; }
}

/// <summary>
/// Describes a queue including its consumer registrations, exchange bindings, and runtime settings.
/// </summary>
/// <param name="Name">The initial name of the queue.</param>
public sealed record QueueDefinition(string Name)
{
    private readonly HashSet<Registration> _registrations = [];
    private readonly HashSet<Subscription> _subscriptions = [];
    private RetryPolicyDefinition? _codeDefaultRetryPolicy;

    /// <summary>
    /// Gets or sets the default retry policy applied to all consumers on this queue.
    /// </summary>
    public RetryPolicyDefinition? DefaultRetryPolicy { get; set; }
    /// <summary>
    /// Gets or sets the dead letter configuration for this queue, or <see langword="null"/> if disabled.
    /// </summary>
    public DeadLetterDefinition? DeadLetter { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string Name { get; set; } = Name;
    /// <summary>
    /// Gets or sets the prefetch count (number of unacknowledged messages). Default is 16.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 16;
    /// <summary>
    /// Gets or sets the number of channels to open for this queue. Default is 1.
    /// </summary>
    public ushort ChannelCount { get; set; } = 1;
    /// <summary>
    /// Gets or sets the maximum number of concurrent consumers per channel. Default is 1.
    /// </summary>
    public ushort ConcurrencyLimit { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether this queue uses quorum replication. Default is <see langword="true"/>.
    /// </summary>
    public bool IsQuorum { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether this queue is durable. Default is <see langword="true"/>.
    /// </summary>
    public bool Durable { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether this queue is automatically deleted when the last consumer disconnects.
    /// </summary>
    public bool AutoDelete { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether this queue is exclusive to the declaring connection.
    /// </summary>
    public bool Exclusive { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the queue is declared with RabbitMQ's single active consumer
    /// feature, so that only one consumer processes deliveries at a time (additional consumers stand by and
    /// take over on failure). This preserves per-queue order across load-balanced consumer instances at the
    /// cost of throughput scale-out for the queue. Partitioned queues enable this automatically.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool SingleActiveConsumer { get; set; }

    /// <summary>
    /// Gets or sets the exchange type for the queue's exchange binding. Default is <see cref="MessagingExchangeType.Fanout"/>.
    /// </summary>
    public MessagingExchangeType ExchangeType { get; set; } = MessagingExchangeType.Fanout;
    /// <summary>
    /// Gets or sets a value indicating whether the exchange is durable. Default is <see langword="true"/>.
    /// </summary>
    public bool ExchangeDurable { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether the exchange is automatically deleted when no queues are bound.
    /// </summary>
    public bool ExchangeAutoDelete { get; set; }

    /// <summary>
    /// Gets additional arguments passed to the broker during exchange declaration.
    /// </summary>
    public IDictionary<string, object?> ExchangeArguments { get; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets the collection of consumer-to-message bindings configured for this queue.
    /// </summary>
    public IReadOnlyCollection<Registration> Registrations =>
#if NET10_0_OR_GREATER
        _registrations.AsReadOnly();
#else
        _registrations.ToList().AsReadOnly();
#endif

    /// <summary>
    /// Gets the exchange bindings configured for this queue — each <see cref="Subscription"/> becomes one
    /// <c>exchange→queue</c> binding declared at topology setup time. Populated by
    /// <see cref="IQueueConfigurator.Subscribe{TMessage}"/> and <see cref="IQueueConfigurator.SubscribeAll{TInterface}"/>,
    /// and auto-populated by <c>Build</c> for any consumer whose concrete <c>TMessage</c> isn't explicitly subscribed
    /// (the consumer's routing-key pattern is carried into the subscription).
    /// </summary>
    public IReadOnlyCollection<Subscription> Subscriptions =>
#if NET10_0_OR_GREATER
        _subscriptions.AsReadOnly();
#else
        _subscriptions.ToList().AsReadOnly();
#endif

    /// <summary>
    /// Gets a value indicating whether any retry policy is configured, either at the queue or consumer level.
    /// </summary>
    public bool RetryEnabled => DefaultRetryPolicy is not null ||
                    Registrations.Any(r => r.RetryPolicy is not null);

    /// <summary>
    /// Adds a consumer registration to this queue. Duplicate registrations (by value, including the retry settings)
    /// are ignored, so a queue that is configured in several places can register the same consumer again. A consumer
    /// runs once per delivery, so it has one retry policy per queue: a <see cref="ConsumerRegistration"/> for a
    /// consumer and message type that the queue already registers with a different retry policy throws.
    /// </summary>
    /// <param name="registration">The consumer-to-message binding to add.</param>
    /// <exception cref="InvalidOperationException">
    /// The queue already has a <see cref="ConsumerRegistration"/> for the same consumer and message type with a
    /// different <see cref="Registration.RetryPolicy"/>.
    /// </exception>
    public void AddConsumer(Registration registration)
    {
        if (registration is ConsumerRegistration && _registrations.Any(existing => HasOtherRetryPolicy(existing, registration)))
        {
            throw new InvalidOperationException(
                $"Queue '{Name}' already registers consumer '{registration.ConsumerType.Name}' for message type " +
                $"'{registration.MessageType.Name}' with a different retry policy. A consumer runs once per delivery, so it " +
                "has one retry policy per queue: register the consumer in one place, or use the same retry settings in each place.");
        }

        _registrations.Add(registration);
    }

    /// <summary>
    /// Sets <see cref="DefaultRetryPolicy"/> from code (<see cref="IQueueConfigurator.UseRetry"/>). A policy bound
    /// from configuration is replaced, because code takes precedence; a second policy from code must be equal to the
    /// first, because the queue has one default policy.
    /// </summary>
    /// <param name="policy">The default retry policy built from code.</param>
    /// <exception cref="InvalidOperationException">Code already set a different default retry policy for this queue.</exception>
    internal void UseDefaultRetryPolicy(RetryPolicyDefinition policy)
    {
        if (_codeDefaultRetryPolicy is not null && _codeDefaultRetryPolicy != policy)
        {
            throw new InvalidOperationException(
                $"Queue '{Name}' already has a different default retry policy (UseRetry). The queue has one default " +
                "retry policy: call UseRetry in one place, or use the same retry settings in each place.");
        }

        _codeDefaultRetryPolicy = policy;
        DefaultRetryPolicy = policy;
    }

    private static bool HasOtherRetryPolicy(Registration existing, Registration added) =>
        existing is ConsumerRegistration
        && existing.ConsumerType == added.ConsumerType
        && existing.MessageType == added.MessageType
        && existing.RetryPolicy != added.RetryPolicy;

    /// <summary>
    /// Adds an exchange→queue binding to this queue. Duplicate subscriptions (by value) are ignored.
    /// </summary>
    /// <param name="subscription">The subscription describing the binding to add.</param>
    public void AddSubscription(Subscription subscription)
        => _subscriptions.Add(subscription);
}
