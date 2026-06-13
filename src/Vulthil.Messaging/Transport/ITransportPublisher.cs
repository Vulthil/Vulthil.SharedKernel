using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// A transport's raw publish terminal: it builds the wire envelope and sends it to the broker. The public
/// <see cref="IPublisher"/> registered to callers is a filtering facade that runs the publish pipeline and then
/// delegates to this terminal. Transports register their publisher under this interface instead of
/// <see cref="IPublisher"/>.
/// </summary>
public interface ITransportPublisher
{
    /// <summary>
    /// Publishes <paramref name="message"/> to the broker after applying <paramref name="configureContext"/> to a
    /// fresh publish context.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="configureContext">An optional callback to configure the publish context.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task PublishAsync<TMessage>(TMessage message, Func<IPublishContext, ValueTask>? configureContext = null, CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
