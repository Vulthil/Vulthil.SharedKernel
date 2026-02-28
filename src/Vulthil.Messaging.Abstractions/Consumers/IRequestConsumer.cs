namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Marker interface for request/reply consumers.
/// </summary>
public interface IRequestConsumer;

/// <summary>
/// Defines a consumer that handles a request of type <typeparamref name="TRequest"/> and produces a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The request message type.</typeparam>
/// <typeparam name="TResponse">The response message type.</typeparam>
public interface IRequestConsumer<in TRequest, TResponse> : IRequestConsumer
    where TRequest : notnull
    where TResponse : notnull
{
    /// <summary>
    /// Processes the request and produces a response.
    /// </summary>
    /// <param name="messageContext">The message context containing the request payload and metadata.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task containing the response.</returns>
    Task<TResponse> ConsumeAsync(IMessageContext<TRequest> messageContext, CancellationToken cancellationToken = default);
}
