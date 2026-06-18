using System.Collections.Concurrent;
using System.Reflection;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory implementation of <see cref="IMessageHandlerFactory{THandler}"/>. Binds the open-generic
/// <see cref="InMemoryMessageHandlers"/> builders to concrete type arguments via cached typed delegates, so the
/// reflection cost is paid once per consumer/message shape — the same pattern the RabbitMQ transport uses.
/// </summary>
internal sealed class InMemoryHandlerFactory : IMessageHandlerFactory<InMemoryHandler>
{
    private static readonly MethodInfo _consumerMethod = typeof(InMemoryMessageHandlers)
        .GetMethod(nameof(InMemoryMessageHandlers.ForConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(InMemoryMessageHandlers)}.{nameof(InMemoryMessageHandlers.ForConsumer)} not found.");
    private static readonly MethodInfo _requestMethod = typeof(InMemoryMessageHandlers)
        .GetMethod(nameof(InMemoryMessageHandlers.ForRequestConsumer), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(InMemoryMessageHandlers)}.{nameof(InMemoryMessageHandlers.ForRequestConsumer)} not found.");

    private readonly ConcurrentDictionary<(Type Consumer, Type Message), Func<RetryPolicyDefinition?, InMemoryHandler>> _consumerCache = new();
    private readonly ConcurrentDictionary<(Type Consumer, Type Request, Type Response), Func<InMemoryHandler>> _requestCache = new();

    public HandlerEntry<InMemoryHandler> ForConsumer(Type consumerType, Type messageType, RetryPolicyDefinition? retryPolicy)
    {
        var factory = _consumerCache.GetOrAdd((consumerType, messageType), static key =>
            _consumerMethod.MakeGenericMethod(key.Consumer, key.Message).CreateDelegate<Func<RetryPolicyDefinition?, InMemoryHandler>>());
        return new HandlerEntry<InMemoryHandler>(factory(retryPolicy), HandlerKind.Consumer);
    }

    public HandlerEntry<InMemoryHandler> ForRequestConsumer(Type consumerType, Type requestType, Type responseType, RetryPolicyDefinition? retryPolicy)
    {
        var factory = _requestCache.GetOrAdd((consumerType, requestType, responseType), static key =>
            _requestMethod.MakeGenericMethod(key.Consumer, key.Request, key.Response).CreateDelegate<Func<InMemoryHandler>>());
        return new HandlerEntry<InMemoryHandler>(factory(), HandlerKind.RequestConsumer);
    }
}
