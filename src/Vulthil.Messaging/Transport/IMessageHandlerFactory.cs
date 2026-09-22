using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Builds transport-specific dispatch handlers from the open-generic consumer/message type pairs collected
/// during registration. A transport implements this to bind a registration to its own delivery closure;
/// <see cref="MessageExecutionRegistry{THandler}"/> calls it while assembling execution plans and already knows
/// which consumer contract each registration implements, so the handler itself carries no such tag.
/// </summary>
/// <typeparam name="THandler">The transport-specific handler type produced by the factory.</typeparam>
public interface IMessageHandlerFactory<out THandler>
    where THandler : notnull
{
    /// <summary>
    /// Builds a handler for a one-way consumer.
    /// </summary>
    /// <param name="consumerType">The CLR type of the <c>IConsumer&lt;TMessage&gt;</c> implementation.</param>
    /// <param name="messageType">The CLR type of the consumed message.</param>
    /// <param name="retryPolicy">The retry policy to apply, or <see langword="null"/> to inherit the queue default.</param>
    /// <returns>The handler that runs the consumer for a delivered message.</returns>
    THandler ForConsumer(Type consumerType, Type messageType, RetryPolicyDefinition? retryPolicy);

    /// <summary>
    /// Builds a handler for a request/reply consumer.
    /// </summary>
    /// <param name="consumerType">The CLR type of the <c>IRequestConsumer&lt;TRequest, TResponse&gt;</c> implementation.</param>
    /// <param name="requestType">The CLR type of the consumed request.</param>
    /// <param name="responseType">The CLR type of the produced response.</param>
    /// <param name="retryPolicy">The retry policy to apply, or <see langword="null"/> to inherit the queue default.</param>
    /// <returns>The handler that runs the request consumer for a delivered request and replies.</returns>
    THandler ForRequestConsumer(Type consumerType, Type requestType, Type responseType, RetryPolicyDefinition? retryPolicy);
}
