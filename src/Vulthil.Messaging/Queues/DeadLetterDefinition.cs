namespace Vulthil.Messaging.Queues;

/// <summary>
/// Describes the dead letter queue and exchange configuration.
/// </summary>
public sealed record DeadLetterDefinition
{
    /// <summary>
    /// Gets or sets the dead letter queue name, or <see langword="null"/> to use the default.
    /// </summary>
    public string? QueueName { get; set; }
    /// <summary>
    /// Gets or sets the dead letter exchange name, or <see langword="null"/> to use the default.
    /// </summary>
    public string? ExchangeName { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether dead letter routing is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
