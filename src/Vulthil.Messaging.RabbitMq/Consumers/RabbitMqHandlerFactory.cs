using System.Collections.Concurrent;
using System.Reflection;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// RabbitMQ implementation of <see cref="IMessageHandlerFactory{THandler}"/>. Binds the open-generic
/// <see cref="MessageHandlerFactory"/> factory methods to concrete type arguments via cached typed delegates,
/// so the reflection cost is paid once per consumer/message shape.
/// </summary>
internal sealed class RabbitMqHandlerFactory : IMessageHandlerFactory<MessageHandler>
{
    private static readonly MethodInfo _forConsumerMethod = typeof(MessageHandlerFactory)
        .GetMethod(nameof(MessageHandlerFactory.ForConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(MessageHandlerFactory)}.{nameof(MessageHandlerFactory.ForConsumer)} not found.");
    private static readonly MethodInfo _forRequestConsumerMethod = typeof(MessageHandlerFactory)
        .GetMethod(nameof(MessageHandlerFactory.ForRequestConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(MessageHandlerFactory)}.{nameof(MessageHandlerFactory.ForRequestConsumer)} not found.");

    private readonly ConcurrentDictionary<(Type Consumer, Type Message), Func<RetryPolicyDefinition?, MessageHandler>> _consumerFactoryCache = new();
    private readonly ConcurrentDictionary<(Type Consumer, Type Request, Type Response), Func<RetryPolicyDefinition?, MessageHandler>> _requestConsumerFactoryCache = new();

    public HandlerEntry<MessageHandler> ForConsumer(Type consumerType, Type messageType, RetryPolicyDefinition? retryPolicy)
    {
        var factory = _consumerFactoryCache.GetOrAdd((consumerType, messageType), static key =>
            _forConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Message)
                .CreateDelegate<Func<RetryPolicyDefinition?, MessageHandler>>());
        return new HandlerEntry<MessageHandler>(factory(retryPolicy), HandlerKind.Consumer);
    }

    public HandlerEntry<MessageHandler> ForRequestConsumer(Type consumerType, Type requestType, Type responseType, RetryPolicyDefinition? retryPolicy)
    {
        var factory = _requestConsumerFactoryCache.GetOrAdd((consumerType, requestType, responseType), static key =>
            _forRequestConsumerMethod
                .MakeGenericMethod(key.Consumer, key.Request, key.Response)
                .CreateDelegate<Func<RetryPolicyDefinition?, MessageHandler>>());
        return new HandlerEntry<MessageHandler>(factory(retryPolicy), HandlerKind.RequestConsumer);
    }
}
