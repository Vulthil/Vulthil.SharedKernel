using System.Collections.Concurrent;
using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Backing store for the messaging configuration. Implements <see cref="IMessagingOptionsConfigurator"/>
/// for the write-side surface exposed during composition and <see cref="IMessageConfigurationProvider"/>
/// for the read-side surface consumed at runtime by transports, consumers, and filters. The type/URN caches
/// are concurrent because singleton publishers register unconfigured message types lazily, so first publishes
/// of distinct types can write them in parallel.
/// </summary>
internal sealed class MessagingOptions : IMessagingOptionsConfigurator, IMessageConfigurationProvider
{
    public const string SectionName = "Messaging:Options";


    private readonly ConcurrentDictionary<Type, MessageConfiguration> _typeConfigurations = new();
    private readonly ConcurrentDictionary<Uri, Type> _urnToType = new();
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

        var registered = _typeConfigurations.GetOrAdd(messageType, ResolveConfiguration);
        IndexUrn(messageType, registered.Urn);
        return registered;
    }

    /// <summary>
    /// Resolves the configuration for a message type that has no cached entry yet: walks the type hierarchy for
    /// the nearest explicitly configured base, falling back to a fresh default configuration. Pure — safe to run
    /// concurrently as a <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,Func{TKey,TValue})"/> factory.
    /// </summary>
    private MessageConfiguration ResolveConfiguration(Type messageType)
    {
        var current = messageType;
        while (current != null && current != typeof(object))
        {
            if (current.FullName is { } fullName && MessageConfigurations.TryGetValue(fullName, out var configured))
            {
                return configured;
            }

            current = current.BaseType;
        }

        return new MessageConfiguration(messageType.FullName!);
    }

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
    /// An explicit registration overwrites a prior one for the same type, so composition can upgrade a
    /// lazily-created default to a configured instance.
    /// </summary>
    internal void RegisterMessageType(Type type, MessageConfiguration configuration)
    {
        if (_typeConfigurations.TryGetValue(type, out var existing) && ReferenceEquals(existing, configuration))
        {
            return;
        }

        _typeConfigurations[type] = configuration;
        IndexUrn(type, configuration.Urn);
    }

    /// <summary>
    /// Records the URN → CLR type reverse mapping. Idempotent for the same pair; throws when the URN is
    /// already owned by a different type.
    /// </summary>
    private void IndexUrn(Type type, Uri urn)
    {
        var owner = _urnToType.GetOrAdd(urn, type);
        if (owner != type)
        {
            throw new InvalidOperationException(
                $"URN '{urn}' is already registered to type '{owner.FullName}'; cannot also register '{type.FullName}'. " +
                "URNs must be unique across message types — override one via MessageConfiguration<T>.Urn.");
        }
    }
}
