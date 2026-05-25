using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Backing store for the messaging configuration. Implements <see cref="IMessagingOptionsConfigurator"/>
/// for the write-side surface exposed during composition and <see cref="IMessageConfigurationProvider"/>
/// for the read-side surface consumed at runtime by transports, consumers, and filters.
/// </summary>
internal sealed class MessagingOptions : IMessagingOptionsConfigurator, IMessageConfigurationProvider
{
    public const string SectionName = "Messaging:Options";

    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public string FaultExchangeName { get; set; } = "Fault.Exchange";

    public ConsumeFilterOptions ConsumeFilters { get; } = new();

    private readonly HashSet<MessageType> _registeredRequestTypes = [];

    internal Dictionary<string, MessageConfiguration> MessageConfigurations { get; } = new(StringComparer.Ordinal);

    internal Dictionary<string, QueueDefinition> QueueDefinitions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public MessageConfiguration GetMessageConfiguration(Type messageType)
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

    /// <inheritdoc />
    public MessageConfiguration GetMessageConfiguration<TMessage>() where TMessage : class
        => GetMessageConfiguration(typeof(TMessage));

    /// <inheritdoc />
    IReadOnlyCollection<QueueDefinition> IMessageConfigurationProvider.QueueDefinitions => QueueDefinitions.Values;

    internal bool RegisterRequestType(MessageType messageType) => _registeredRequestTypes.Add(messageType);
}
