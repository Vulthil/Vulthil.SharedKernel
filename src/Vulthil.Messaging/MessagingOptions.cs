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
    /// Gets or sets the name of the exchange to which faults are published when a consumed message carries a <c>FaultAddress</c> header.
    /// Default is <c>"Fault.Exchange"</c>.
    /// </summary>
    public string FaultExchangeName { get; set; } = "Fault.Exchange";

    /// <summary>
    /// Gets the options that control which built-in consume filters are active.
    /// </summary>
    public ConsumeFilterOptions ConsumeFilters { get; } = new();

    /// <summary>
    /// Message configurations keyed by the CLR full type name. Populated eagerly from <c>Messaging:Messages:*</c>
    /// at <c>AddMessaging</c> time, then merged with whatever <c>ConfigureMessage&lt;T&gt;</c> registers in code.
    /// </summary>
    internal Dictionary<string, MessageConfiguration> MessageConfigurations { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Queue definitions keyed by queue name. Populated eagerly from <c>Messaging:Queues:*</c> at <c>AddMessaging</c>
    /// time, then merged with whatever <c>ConfigureQueue</c> registers in code.
    /// </summary>
    internal Dictionary<string, QueueDefinition> QueueDefinitions { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal MessageConfiguration GetMessageConfiguration(Type messageType)
    {
        var current = messageType;
        while (current != null && current != typeof(object))
        {
            if (current.FullName is { } fullName && MessageConfigurations.TryGetValue(fullName, out var def))
            {
                return def;
            }

            current = current.BaseType;
        }

        return new MessageConfiguration(messageType.FullName!);
    }

    internal MessageConfiguration GetMessageConfiguration<TMessage>() =>
        GetMessageConfiguration(typeof(TMessage));

    internal bool RegisterRequestType(MessageType messageType) => _registeredRequestTypes.Add(messageType);
}
