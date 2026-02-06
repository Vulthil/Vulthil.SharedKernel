using System.Text.Json;

namespace Vulthil.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging:Options";
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    internal Dictionary<Type, Func<object, string>> RoutingKeyFormatters { get; } = [];
    internal Dictionary<Type, Func<object, string>> CorrelationIdFormatters { get; } = [];

    public IReadOnlyDictionary<Type, Func<object, string>> ReadOnlyRoutingKeyFormatters => RoutingKeyFormatters;
    public IReadOnlyDictionary<Type, Func<object, string>> ReadOnlyCorrelationIdFormatters => CorrelationIdFormatters;
}
