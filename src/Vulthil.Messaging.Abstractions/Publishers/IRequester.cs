using Vulthil.Results;

namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// Sends a request message and awaits a response from a remote consumer.
/// </summary>
public interface IRequester
{
    /// <summary>
    /// Sends a request and awaits a response, returning the outcome as a <see cref="Result{TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="message">The request message to send.</param>
    /// <param name="configureContext">An optional action to configure the publish context.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A <see cref="Result{TResponse}"/> containing the response on success or an error on failure.</returns>
    Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
        TRequest message,
        Func<IPublishContext, Task>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull;
}
