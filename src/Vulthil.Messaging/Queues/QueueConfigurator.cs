using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

internal sealed class QueueConfigurator(IServiceCollection services, MessagingOptions messagingOptions, QueueDefinition queueDefinition) : IQueueConfigurator
{
    private readonly IServiceCollection _services = services;
    private readonly MessagingOptions _messagingOptions = messagingOptions;
    private readonly QueueDefinition _queueDefinition = queueDefinition;

    /// <inheritdoc />
    public IQueueConfigurator ConfigureQueue(Action<QueueDefinition> configureAction)
    {
        configureAction(_queueDefinition);
        return this;
    }

    /// <inheritdoc />
    public IQueueConfigurator AddConsumer<TConsumer>(Action<IConsumerConfigurator<TConsumer>>? configure = null)
       where TConsumer : class, IConsumer
    {
        var configurator = new ConsumerConfigurator<TConsumer>();
        configure?.Invoke(configurator);

        var consumerType = new ConsumerType(typeof(TConsumer));
        _services.TryAddScoped<TConsumer>();

        var standardInterfaces = consumerType.Type.GetInterfaces()
            .Where(i => i.IsGenericType && !i.IsGenericTypeDefinition)
                .Where(i => i.GetGenericTypeDefinition() == typeof(IConsumer<>));

        foreach (var i in standardInterfaces)
        {
            var messageType = new MessageType(i.GetGenericArguments()[0]);

            _queueDefinition.AddConsumer(new ConsumerRegistration
            {
                ConsumerType = consumerType,
                MessageType = messageType,
                RetryPolicy = configurator.RetryPolicy
            });
        }

        return this;
    }

    /// <inheritdoc />
    public IQueueConfigurator AddRequestConsumer<TConsumer>(Action<IRequestConfigurator<TConsumer>>? configure = null)
        where TConsumer : class, IRequestConsumer
    {
        var configurator = new RequestConsumerConfigurator<TConsumer>();
        configure?.Invoke(configurator);

        if (configurator.RetryPolicy is not null)
        {
            throw new InvalidOperationException(
                $"Request consumer '{typeof(TConsumer).FullName}' configures UseRetry, but request consumers do not retry: " +
                "a thrown exception is immediately returned to the requester as an RPC fault reply, so the policy would never run. " +
                "Remove UseRetry from AddRequestConsumer and retry on the requesting side if needed.");
        }

        var consumerType = new ConsumerType(typeof(TConsumer));
        _services.TryAddScoped<TConsumer>();

        var requestInterfaces = consumerType.Type.GetInterfaces()
            .Where(i => i.IsGenericType && !i.IsGenericTypeDefinition)
            .Where(i => i.GetGenericTypeDefinition() == typeof(IRequestConsumer<,>));

        foreach (var i in requestInterfaces)
        {
            var args = i.GetGenericArguments();
            var reqType = new MessageType(args[0]);
            var resType = args[1];

            if (!_messagingOptions.RegisterRequestType(reqType))
            {
                throw new InvalidOperationException($"Request '{reqType.Name}' is already handled elsewhere.");
            }

            _queueDefinition.AddConsumer(new RequestConsumerRegistration
            {
                ConsumerType = consumerType,
                MessageType = reqType,
                ResponseType = resType,
                RetryPolicy = configurator.RetryPolicy
            });
        }

        return this;
    }

    /// <inheritdoc />
    public IQueueConfigurator Subscribe<TMessage>(string? routingKey = null) where TMessage : class
        => SubscribeCore(typeof(TMessage), routingKey);

    /// <inheritdoc />
    public IQueueConfigurator SubscribeAll<TInterface>(Assembly assembly, string? routingKey = null) where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var concrete in assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(TInterface).IsAssignableFrom(t)))
        {
            SubscribeCore(concrete, routingKey);
        }
        return this;
    }

    private QueueConfigurator SubscribeCore(Type concrete, string? routingKey)
    {
        if (concrete.IsAbstract || concrete.IsInterface)
        {
            throw new InvalidOperationException(
                $"Cannot Subscribe to abstract or interface type '{concrete.FullName}'. " +
                "Only concrete message types have exchanges; use SubscribeAll<TInterface>(assembly) to discover implementers " +
                "or call Subscribe<TConcrete>() for each one.");
        }

        _messagingOptions.GetMessageConfiguration(concrete);
        _queueDefinition.AddSubscription(new Subscription(new MessageType(concrete), routingKey));
        return this;
    }

    /// <inheritdoc />
    public IQueueConfigurator UseRetry(Action<RetryPolicyConfigurator> configure)
    {
        var builder = new RetryPolicyConfigurator();
        configure(builder);

        _queueDefinition.DefaultRetryPolicy = builder.Build();

        return this;
    }

    /// <inheritdoc />
    public IQueueConfigurator UseDeadLetterQueue(string? queueName = null, string? exchangeName = null)
    {
        _queueDefinition.DeadLetter = new DeadLetterDefinition
        {
            Enabled = true,
            QueueName = queueName,
            ExchangeName = exchangeName
        };

        return this;
    }

    /// <inheritdoc />
    public IQueueConfigurator UseSingleActiveConsumer()
    {
        _queueDefinition.SingleActiveConsumer = true;
        return this;
    }

    /// <summary>
    /// Final resolution pass — runs once after the user's configurator action completes. Auto-subscribes
    /// any concrete TMessage from consumer registrations that wasn't explicitly subscribed, and validates
    /// that every consumer has at least one matching concrete subscription and every concrete subscription
    /// has at least one matching consumer. Request consumers must target concrete types.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a consumer has no matching subscription, a subscription has no matching consumer,
    /// or a request consumer is registered against an abstract/interface request type.
    /// </exception>
    internal void Build()
    {
        // Routing keys are a Subscription-level concern, so auto-subscribed subscriptions get a null routing
        // key (broker uses an empty pattern). For direct/topic exchanges that require a specific pattern, the
        // caller must explicitly q.Subscribe<TConcrete>("pattern") first.
        var concreteConsumerMessageTypes = _queueDefinition.Registrations
            .Select(r => r.MessageType)
            .Where(m => m.Type is { IsAbstract: false, IsInterface: false });
        foreach (var messageType in concreteConsumerMessageTypes)
        {
            _messagingOptions.GetMessageConfiguration(messageType.Type);
            _queueDefinition.AddSubscription(new Subscription(messageType));
        }

        // Request consumers cannot be polymorphic — the response type is fixed and can't be selected
        // by the incoming concrete type.
        foreach (var rpc in _queueDefinition.Registrations.OfType<RequestConsumerRegistration>())
        {
            var t = rpc.MessageType.Type;
            if (t.IsAbstract || t.IsInterface)
            {
                throw new InvalidOperationException(
                    $"Queue '{_queueDefinition.Name}': request consumer '{rpc.ConsumerType.Name}' has polymorphic request type '{t.FullName}'. " +
                    "Request consumers must use a concrete request type since the response is typed.");
            }
        }

        var orphanConsumer = _queueDefinition.Registrations.FirstOrDefault(
            r => !_queueDefinition.Subscriptions.Any(s => r.MessageType.Type.IsAssignableFrom(s.MessageType.Type)));
        if (orphanConsumer is not null)
        {
            throw new InvalidOperationException(
                $"Queue '{_queueDefinition.Name}': consumer '{orphanConsumer.ConsumerType.Name}' targets message type '{orphanConsumer.MessageType.Type.FullName}' " +
                "but no concrete subscribed type on this queue is assignable to it. " +
                "Call q.Subscribe<TConcrete>() or q.SubscribeAll<TInterface>(assembly) for at least one implementer.");
        }

        var orphanSubscription = _queueDefinition.Subscriptions.FirstOrDefault(
            s => !_queueDefinition.Registrations.Any(r => r.MessageType.Type.IsAssignableFrom(s.MessageType.Type)));
        if (orphanSubscription is not null)
        {
            throw new InvalidOperationException(
                $"Queue '{_queueDefinition.Name}': concrete subscription '{orphanSubscription.MessageType.Type.FullName}' has no matching consumer. " +
                "Either AddConsumer<TConsumer>() that handles this message type, or remove the subscription.");
        }
    }
}
