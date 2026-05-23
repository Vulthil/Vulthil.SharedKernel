namespace Vulthil.Messaging;

/// <summary>
/// Configuration used when publishing or binding a message type.
/// Contains exchange declaration settings and optional formatters for routing keys and correlation ids.
/// </summary>
public record MessageConfiguration
{
    /// <summary>
    /// Initializes a new <see cref="MessageConfiguration"/> for the specified exchange name.
    /// </summary>
    /// <param name="exchange">The name of the exchange to declare and bind for this message type.</param>
    public MessageConfiguration(string exchange) => Exchange = exchange;

    /// <summary>
    /// The name of the exchange to declare and bind for this message type.
    /// Defaults to the message CLR full type name when constructed via <see cref="MessageConfiguration{TMessage}"/>.
    /// </summary>
    public string Exchange { get; set; }

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
/// Configuration options for a specific message type, including exchange, routing key and correlation ID selection.
/// </summary>
/// <typeparam name="TMessage">The message type to configure.</typeparam>
public sealed record MessageConfiguration<TMessage> : MessageConfiguration
    where TMessage : class
{
    /// <summary>
    /// Initializes a new <see cref="MessageConfiguration{TMessage}"/> whose exchange defaults to the CLR full type name of <typeparamref name="TMessage"/>.
    /// </summary>
    public MessageConfiguration() : base(typeof(TMessage).FullName!) { }

    /// <summary>
    /// Configures the routing key using the specified literal value.
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
