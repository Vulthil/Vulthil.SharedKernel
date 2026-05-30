namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// Publishes messages to a message broker.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a message to the broker with optional context configuration.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : notnull;

    /// <summary>
    /// Publishes a message to the broker with optional context configuration.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="configureContext">An optional action to configure the publish context (routing key, headers, etc.).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
