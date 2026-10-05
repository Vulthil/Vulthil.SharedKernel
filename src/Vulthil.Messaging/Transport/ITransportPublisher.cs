using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// A transport's raw publish terminal: it builds the wire envelope and sends it to the broker. The public
/// <see cref="IPublisher"/> registered to callers is a filtering facade: it runs the caller's configure callback and
/// the publish pipeline on one <see cref="PublishContext"/>, then hands the message and that resolved context to this
/// terminal. The outbox relay calls the terminal the same way, with the context it stored. Transports register their
/// publisher under this interface instead of <see cref="IPublisher"/>.
/// </summary>
public interface ITransportPublisher
{
    /// <summary>
    /// Publishes <paramref name="message"/> to the broker with the resolved values of <paramref name="context"/>.
    /// </summary>
    /// <param name="message">
    /// The message to publish. Its runtime type selects the message configuration, and the payload is serialized as
    /// that type.
    /// </param>
    /// <param name="context">The resolved publish context: message id, correlation id, routing key and headers.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the transport has published the message.</returns>
    Task PublishAsync(object message, PublishContext context, CancellationToken cancellationToken);
}
