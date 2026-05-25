using System.Text.Json;

namespace Vulthil.Messaging;

/// <summary>
/// Mutable view of the messaging options used during composition. Passed to the action
/// of <see cref="IMessagingConfigurator.ConfigureMessagingOptions(System.Action{IMessagingOptionsConfigurator})"/>.
/// </summary>
/// <remarks>
/// The same underlying instance is exposed read-only through <see cref="IMessageConfigurationProvider"/>
/// at runtime, so changes made here are observable by transports, consumers, and filters once
/// <c>AddMessaging</c> returns.
/// </remarks>
public interface IMessagingOptionsConfigurator
{
    /// <summary>
    /// Gets or sets the JSON serializer options used for message serialization and deserialization.
    /// </summary>
    JsonSerializerOptions JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets the default timeout for request/reply operations. Default is 30 seconds.
    /// </summary>
    TimeSpan DefaultTimeout { get; set; }

    /// <summary>
    /// Gets or sets the name of the exchange to which faults are published when a consumed message
    /// carries a <c>FaultAddress</c> header. Default is <c>"Fault.Exchange"</c>.
    /// </summary>
    string FaultExchangeName { get; set; }

    /// <summary>
    /// Gets the options that control which built-in consume filters are active.
    /// </summary>
    ConsumeFilterOptions ConsumeFilters { get; }
}
