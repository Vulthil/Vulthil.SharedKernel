namespace Vulthil.Messaging;

/// <summary>
/// Represents configuration used when publishing a message type.
/// Contains exchange declaration settings and optional formatters for routing keys and correlation ids.
/// </summary>
public record MessageConfiguration
{
    /// <summary>
    /// The name of the exchange to publish to. When <c>null</c> the transport may fall back to a convention (for example the message CLR type name).
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// The exchange type to declare when creating the exchange (if applicable to the transport).
    /// </summary>
    public MessagingExchangeType ExchangeType { get; set; } = MessagingExchangeType.Fanout;

    /// <summary>
    /// Whether the declared exchange should be durable. Defaults to <c>true</c>.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Whether the declared exchange should be automatically deleted when no longer in use.
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Optional additional arguments to supply when declaring the exchange.
    /// This collection is read-only; populate it with values rather than replacing the instance.
    /// </summary>
    public IDictionary<string, object?> Arguments { get; } = new Dictionary<string, object?>();

    /// <summary>
    /// Optional formatter used to produce the routing key for a message instance.
    /// When set this will be used by transports instead of separately-registered routing formatters.
    /// </summary>
    public Func<object, string>? RoutingKeyFormatter { get; set; }

    /// <summary>
    /// Optional formatter used to produce the correlation identifier for a message instance.
    /// When set this will be used by transports instead of separately-registered correlation formatters.
    /// </summary>
    public Func<object, string>? CorrelationIdFormatter { get; set; }
}

/// <summary>
/// Represents configuration options for a specific message type, including routing key and correlation ID selection.
/// </summary>
/// <typeparam name="TMessage">The message type to configure.</typeparam>
public sealed record MessageConfiguration<TMessage> : MessageConfiguration
    where TMessage : class
{

    /// <summary>
    /// Configures the routing key using the specified selector function.
    /// </summary>
    /// <param name="routingKey">The routing key to use for the message.</param>
    /// <returns>The current message configuration instance.</returns>
    public MessageConfiguration<TMessage> UseRoutingKey(string routingKey) => UseRoutingKey(_ => routingKey);

    /// <summary>
    /// Configures the routing key using the specified selector function.
    /// </summary>
    /// <param name="selector">A function that selects the routing key from the message.</param>
    /// <returns>The current message configuration instance.</returns>
    public MessageConfiguration<TMessage> UseRoutingKey(Func<TMessage, string> selector)
    {
        RoutingKeyFormatter = message => selector((TMessage)message);
        return this;

    }
    /// <summary>
    /// Configures the correlation ID for the message using the specified selector function.
    /// </summary>
    /// <param name="selector">A function that extracts the correlation ID from the message.</param>
    /// <returns>The current message configuration instance.</returns>
    public MessageConfiguration<TMessage> UseCorrelationId(Func<TMessage, string> selector)
    {
        CorrelationIdFormatter = message => selector((TMessage)message);
        return this;
    }
}
