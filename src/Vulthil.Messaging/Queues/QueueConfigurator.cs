using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

internal sealed class QueueConfigurator(IServiceCollection services, QueueDefinition queueDefinition) : IQueueConfigurator
{
    private static readonly HashSet<MessageType> _registeredRequestTypes = [];

    private readonly IServiceCollection _services = services;
    private readonly QueueDefinition _queueDefinition = queueDefinition;

    public IQueueConfigurator ConfigureQueue(Action<QueueDefinition> configureAction)
    {
        configureAction(_queueDefinition);
        return this;
    }

    public IQueueConfigurator AddConsumer<TConsumer>(Action<ConsumerConfigurator<TConsumer>>? configure = null)
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

            // Use the override if it exists, otherwise default to "#"
            var routingKey = configurator.Overrides.GetValueOrDefault(messageType, "#");

            _queueDefinition.AddConsumer(new ConsumerRegistration
            {
                ConsumerType = consumerType,
                MessageType = messageType,
                RoutingKey = routingKey
            });
        }

        return this;
    }

    public IQueueConfigurator AddRequestConsumer<TConsumer>(Action<RequestConsumerConfigurator<TConsumer>>? configure = null)
        where TConsumer : class, IRequestConsumer
    {
        var configurator = new RequestConsumerConfigurator<TConsumer>();
        configure?.Invoke(configurator);

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

            // Preservation of your original validation
            if (_registeredRequestTypes.Contains(reqType))
            {
                throw new InvalidOperationException($"Request '{reqType.Name}' is already handled elsewhere.");
            }
            _registeredRequestTypes.Add(reqType);

            var routingKey = configurator.Overrides.GetValueOrDefault(reqType, "#");

            _queueDefinition.AddConsumer(new RequestConsumerRegistration
            {
                ConsumerType = consumerType,
                MessageType = reqType,
                ResponseType = resType,
                RoutingKey = routingKey
            });
        }

        return this;
    }
}
