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


    private readonly Dictionary<Type, MessageConfiguration> _typeConfigurations = [];
    private readonly Dictionary<Uri, Type> _urnToType = [];
    private readonly HashSet<MessageType> _registeredRequestTypes = [];
    private readonly Dictionary<Type, PartitionSpec> _partitions = [];
    internal Dictionary<string, MessageConfiguration> MessageConfigurations { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, QueueDefinition> QueueDefinitions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public string FaultExchangeName { get; set; } = "Fault.Exchange";
    public ConsumeFilterOptions ConsumeFilters { get; } = new();

    /// <inheritdoc />
    public MessageConfiguration GetMessageConfiguration(Type messageType)
    {
        if (_typeConfigurations.TryGetValue(messageType, out var cached))
        {
            return cached;
        }

        var current = messageType;
        while (current != null && current != typeof(object))
        {
            if (current.FullName is { } fullName && MessageConfigurations.TryGetValue(fullName, out var def))
            {
                RegisterMessageType(messageType, def);
                return def;
            }

            current = current.BaseType;
        }

        var fresh = new MessageConfiguration(messageType.FullName!);
        RegisterMessageType(messageType, fresh);
        return fresh;
    }

    /// <inheritdoc />
    public MessageConfiguration GetMessageConfiguration<TMessage>() where TMessage : class
        => GetMessageConfiguration(typeof(TMessage));

    /// <inheritdoc />
    public Uri GetUrn(Type messageType) => GetMessageConfiguration(messageType).Urn;

    /// <inheritdoc />
    public Type? GetMessageType(Uri urn) => _urnToType.GetValueOrDefault(urn);

    /// <inheritdoc />
    IReadOnlyCollection<QueueDefinition> IMessageConfigurationProvider.QueueDefinitions => QueueDefinitions.Values;

    internal bool RegisterRequestType(MessageType messageType) => _registeredRequestTypes.Add(messageType);

    /// <summary>Records the partition configuration for a message type (overwrites any prior registration).</summary>
    internal void RegisterPartition(Type messageType, PartitionSpec spec) => _partitions[messageType] = spec;

    /// <inheritdoc />
    public PartitionSpec? GetPartition(Type messageType) => _partitions.GetValueOrDefault(messageType);

    /// <summary>
    /// Records a CLR type ↔ <see cref="MessageConfiguration"/> mapping and updates the URN reverse index.
    /// Idempotent on repeated calls for the same type; throws if two distinct types claim the same URN.
    /// </summary>
    internal void RegisterMessageType(Type type, MessageConfiguration configuration)
    {
        if (_typeConfigurations.TryGetValue(type, out var existing) && ReferenceEquals(existing, configuration))
        {
            return;
        }

        _typeConfigurations[type] = configuration;

        if (_urnToType.TryGetValue(configuration.Urn, out var owner))
        {
            if (owner != type)
            {
                throw new InvalidOperationException(
                    $"URN '{configuration.Urn}' is already registered to type '{owner.FullName}'; cannot also register '{type.FullName}'. " +
                    "URNs must be unique across message types — override one via MessageConfiguration<T>.Urn.");
            }
            return;
        }

        _urnToType[configuration.Urn] = type;
    }
}
