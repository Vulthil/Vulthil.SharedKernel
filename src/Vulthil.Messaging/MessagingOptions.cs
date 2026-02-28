using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Global messaging configuration options including serialization, timeouts, and fault handling.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>
    /// The configuration section name used for binding messaging options.
    /// </summary>
    public const string SectionName = "Messaging:Options";
    /// <summary>
    /// Gets or sets the JSON serializer options used for message serialization and deserialization.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();

    private readonly HashSet<MessageType> _registeredRequestTypes = [];

    /// <summary>
    /// Gets or sets the default timeout for request/reply operations. Default is 30 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>
    /// Gets or sets the name of the fault exchange. Default is "Fault.Exchange".
    /// </summary>
    public string FaultExchangeName { get; set; } = "Fault.Exchange";
    /// <summary>
    /// Gets or sets a value indicating whether the fault exchange is automatically declared. Default is <see langword="true"/>.
    /// </summary>
    public bool AutoDeclareFaultStatus { get; set; } = true;

    internal Dictionary<Type, Func<object, string>> RoutingKeyFormatters { get; } = [];
    internal Dictionary<Type, Func<object, string>> CorrelationIdFormatters { get; } = [];

    internal bool RegisterRequestType(MessageType messageType) => _registeredRequestTypes.Add(messageType);

    /// <summary>
    /// Gets the read-only collection of registered routing key formatters, keyed by message type.
    /// Used by the transport to determine the routing key when publishing a message.
    /// </summary>
    public IReadOnlyDictionary<Type, Func<object, string>> ReadOnlyRoutingKeyFormatters => RoutingKeyFormatters;
    /// <summary>
    /// Gets the read-only collection of registered correlation identifier formatters, keyed by message type.
    /// Used by the transport to set the correlation ID when publishing a message.
    /// </summary>
    public IReadOnlyDictionary<Type, Func<object, string>> ReadOnlyCorrelationIdFormatters => CorrelationIdFormatters;
}
