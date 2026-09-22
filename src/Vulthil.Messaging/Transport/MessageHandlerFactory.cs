using System.Collections.Concurrent;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Base <see cref="IMessageHandlerFactory{THandler}"/> for a transport that builds its dispatch handlers in
/// open-generic methods. Override <see cref="CreateConsumerHandler{TConsumer, TMessage}"/> and
/// <see cref="CreateRequestConsumerHandler{TConsumer, TRequest, TResponse}"/>: the consumer and message types
/// are statically known there, so the receive context and the consume pipeline compose with full typing. This
/// class binds the CLR types of each registration to those overrides through generic binders cached per
/// consumer/message shape, so the reflection cost is paid once per shape. Implement
/// <see cref="IMessageHandlerFactory{THandler}"/> directly instead when handlers are not built generically.
/// </summary>
/// <typeparam name="THandler">The transport-specific handler type stored in execution plans.</typeparam>
public abstract class MessageHandlerFactory<THandler> : IMessageHandlerFactory<THandler>
    where THandler : notnull
{
    private readonly ConcurrentDictionary<(Type Consumer, Type Message), Binder> _consumerBinders = new();
    private readonly ConcurrentDictionary<(Type Consumer, Type Request, Type Response), Binder> _requestConsumerBinders = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="consumerType"/> does not implement <c>IConsumer&lt;TMessage&gt;</c> for <paramref name="messageType"/>.</exception>
    public THandler ForConsumer(Type consumerType, Type messageType, RetryPolicyDefinition? retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentNullException.ThrowIfNull(messageType);

        var binder = _consumerBinders.GetOrAdd(
            (consumerType, messageType),
            static key => Binder.Create(typeof(MessageHandlerFactory<>.ConsumerBinder<,>), key.Consumer, key.Message));
        return binder.Build(this, retryPolicy);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="consumerType"/> does not implement <c>IRequestConsumer&lt;TRequest, TResponse&gt;</c> for <paramref name="requestType"/> and <paramref name="responseType"/>.</exception>
    public THandler ForRequestConsumer(Type consumerType, Type requestType, Type responseType, RetryPolicyDefinition? retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);

        var binder = _requestConsumerBinders.GetOrAdd(
            (consumerType, requestType, responseType),
            static key => Binder.Create(typeof(MessageHandlerFactory<>.RequestConsumerBinder<,,>), key.Consumer, key.Request, key.Response));
        return binder.Build(this, retryPolicy);
    }

    /// <summary>
    /// Builds the handler for a one-way <see cref="IConsumer{TMessage}"/> registration. Called once per registration
    /// while <see cref="MessageExecutionRegistry{THandler}"/> assembles execution plans.
    /// </summary>
    /// <typeparam name="TConsumer">The consumer implementation.</typeparam>
    /// <typeparam name="TMessage">The message type the registration consumes.</typeparam>
    /// <param name="retryPolicy">
    /// The registration's effective retry policy, or <see langword="null"/> when the handler fails terminally on its
    /// first failure.
    /// </param>
    /// <returns>The handler that runs <typeparamref name="TConsumer"/> for a delivered <typeparamref name="TMessage"/>.</returns>
    protected abstract THandler CreateConsumerHandler<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull;

    /// <summary>
    /// Builds the handler for a request/reply <see cref="IRequestConsumer{TRequest, TResponse}"/> registration.
    /// Called once per registration while <see cref="MessageExecutionRegistry{THandler}"/> assembles execution plans.
    /// </summary>
    /// <typeparam name="TConsumer">The request consumer implementation.</typeparam>
    /// <typeparam name="TRequest">The consumed request type.</typeparam>
    /// <typeparam name="TResponse">The produced response type.</typeparam>
    /// <param name="retryPolicy">
    /// Always <see langword="null"/> when called by <see cref="MessageExecutionRegistry{THandler}"/>: a request
    /// consumer replies with an RPC fault instead of retrying.
    /// </param>
    /// <returns>The handler that runs <typeparamref name="TConsumer"/> for a delivered <typeparamref name="TRequest"/> and replies.</returns>
    protected abstract THandler CreateRequestConsumerHandler<TConsumer, TRequest, TResponse>(RetryPolicyDefinition? retryPolicy)
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull;

    /// <summary>
    /// Closes one of the generic binder types over a registration's CLR types. The binder calls the typed override
    /// as ordinary C#, so the generic constraints are checked here (once per shape) and the call itself needs no
    /// further reflection.
    /// </summary>
    private abstract class Binder
    {
        public abstract THandler Build(MessageHandlerFactory<THandler> factory, RetryPolicyDefinition? retryPolicy);

        public static Binder Create(Type unboundBinder, params Type[] registrationTypes)
        {
            var typeArguments = new Type[registrationTypes.Length + 1];
            typeArguments[0] = typeof(THandler);
            registrationTypes.CopyTo(typeArguments, 1);
            return (Binder)Activator.CreateInstance(unboundBinder.MakeGenericType(typeArguments))!;
        }
    }

    private sealed class ConsumerBinder<TConsumer, TMessage> : Binder
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
    {
        public override THandler Build(MessageHandlerFactory<THandler> factory, RetryPolicyDefinition? retryPolicy)
            => factory.CreateConsumerHandler<TConsumer, TMessage>(retryPolicy);
    }

    private sealed class RequestConsumerBinder<TConsumer, TRequest, TResponse> : Binder
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
    {
        public override THandler Build(MessageHandlerFactory<THandler> factory, RetryPolicyDefinition? retryPolicy)
            => factory.CreateRequestConsumerHandler<TConsumer, TRequest, TResponse>(retryPolicy);
    }
}
