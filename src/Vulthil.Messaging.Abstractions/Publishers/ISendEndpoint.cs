namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// Resolves <see cref="ISendEndpoint"/> instances for point-to-point message delivery.
/// </summary>
/// <remarks>
/// Unlike <see cref="IPublisher"/> (which fans out a message via its configured exchange to any
/// number of interested consumers), a send endpoint addresses a single, named destination — typically
/// a specific queue on a specific service. The destination is identified by a <see cref="System.Uri"/>;
/// the default scheme recognized by the RabbitMQ transport is <c>queue:&lt;name&gt;</c>.
/// </remarks>
public interface ISendEndpointProvider
{
    /// <summary>
    /// Resolves an <see cref="ISendEndpoint"/> for the supplied destination address.
    /// </summary>
    /// <param name="address">The destination address (e.g. <c>queue:order-commands</c>).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The endpoint bound to <paramref name="address"/>. Implementations may cache and share endpoints across calls.</returns>
    ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends messages to a single, addressable destination (point-to-point delivery).
/// </summary>
/// <remarks>
/// Sends are addressed directly to the destination identified by <see cref="Address"/> and bypass the
/// per-message-type exchange used by <see cref="IPublisher"/>. Topology for the destination queue is owned
/// by the receiving service; the sender does not declare it.
/// </remarks>
public interface ISendEndpoint
{
    /// <summary>
    /// Gets the destination address this endpoint sends to.
    /// </summary>
    Uri Address { get; }

    /// <summary>
    /// Sends a message to <see cref="Address"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to send.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SendAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : notnull;

    /// <summary>
    /// Sends a message to <see cref="Address"/> with optional context configuration.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to send.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="configureContext">An optional callback for configuring the publish context (correlation, headers, etc.).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SendAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
