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

    internal Dictionary<Type, MessageConfiguration> MessageConfigurations { get; } = [];

    internal MessageConfiguration GetMessageConfiguration(Type messageType)
    {
        var current = messageType;
        while (current != null && current != typeof(object))
        {
            if (MessageConfigurations.TryGetValue(current, out var def))
            {
                return def;
            }

            current = current.BaseType;
        }

        return new MessageConfiguration();
    }

    internal MessageConfiguration GetMessageConfiguration<TMessage>() =>
        GetMessageConfiguration(typeof(TMessage));

    internal bool RegisterRequestType(MessageType messageType) => _registeredRequestTypes.Add(messageType);

}
