using System.ComponentModel.DataAnnotations;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ transport tuning options. Bound from the <c>Messaging:RabbitMq</c> configuration section and
/// optionally overridden in code via the <c>configureTransport</c> callback on
/// <see cref="MessagingConfiguratorExtensions.UseRabbitMq"/> (code takes precedence over configuration).
/// </summary>
public sealed class RabbitMqTransportOptions
{
    private const int DefaultPublishChannelPoolSize = 10;

    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Messaging:RabbitMq";

    /// <summary>
    /// Gets or sets the maximum number of channels the publisher pools for concurrent publishing. Each channel
    /// awaits its own publisher confirm, so a larger pool allows more in-flight publishes at the cost of more
    /// broker channels. Must be at least 1. Default is 10.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int PublishChannelPoolSize { get; set; } = DefaultPublishChannelPoolSize;
}
