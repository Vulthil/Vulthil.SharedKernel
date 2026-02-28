using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging:Options";
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();

    private readonly HashSet<MessageType> _registeredRequestTypes = [];

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public string FaultExchangeName { get; set; } = "Fault.Exchange";
    public bool AutoDeclareFaultStatus { get; set; } = true;

    internal Dictionary<Type, Func<object, string>> RoutingKeyFormatters { get; } = [];
    internal Dictionary<Type, Func<object, string>> CorrelationIdFormatters { get; } = [];

    internal bool RegisterRequestType(MessageType messageType) => _registeredRequestTypes.Add(messageType);

    public IReadOnlyDictionary<Type, Func<object, string>> ReadOnlyRoutingKeyFormatters => RoutingKeyFormatters;
    public IReadOnlyDictionary<Type, Func<object, string>> ReadOnlyCorrelationIdFormatters => CorrelationIdFormatters;
}
