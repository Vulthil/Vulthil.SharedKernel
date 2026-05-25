namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Marker interface for message consumers.
/// </summary>
public interface IConsumer;

/// <summary>
/// Defines a consumer that processes messages of type <typeparamref name="TMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
public interface IConsumer<in TMessage> : IConsumer
{
    /// <summary>
    /// Processes the received message.
    /// </summary>
    /// <param name="messageContext">The message context containing the payload and metadata.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConsumeAsync(IMessageContext<TMessage> messageContext, CancellationToken cancellationToken = default);
}
