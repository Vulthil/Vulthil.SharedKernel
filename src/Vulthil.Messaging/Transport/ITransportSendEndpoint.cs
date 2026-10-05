using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// A transport's raw send terminal for one destination: it builds the wire envelope and sends it to
/// <see cref="Address"/>. The public <see cref="ISendEndpoint"/> handed to callers is a filtering facade: it runs the
/// caller's configure callback and the publish pipeline on one <see cref="PublishContext"/>, then hands the message
/// and that resolved context to this terminal. The outbox relay calls the terminal the same way, with the context it
/// stored.
/// </summary>
public interface ITransportSendEndpoint
{
    /// <summary>
    /// Gets the destination address this endpoint sends to.
    /// </summary>
    Uri Address { get; }

    /// <summary>
    /// Sends <paramref name="message"/> to <see cref="Address"/> with the resolved values of
    /// <paramref name="context"/>.
    /// </summary>
    /// <param name="message">
    /// The message to send. Its runtime type selects the message configuration, and the payload is serialized as that
    /// type.
    /// </param>
    /// <param name="context">The resolved publish context: message id, correlation id and headers.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the transport has sent the message.</returns>
    Task SendAsync(object message, PublishContext context, CancellationToken cancellationToken);
}
