namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// An immutable, serializable snapshot of an <see cref="IMessageContext"/>'s transport metadata captured at a
/// point in time (e.g. when a <see cref="Fault{TMessage}"/> is produced). Unlike <see cref="IMessageContext"/>
/// it carries no behavior and no live transport binding, so it round-trips cleanly through serialization.
/// </summary>
public sealed record MessageContextSnapshot
{
    /// <summary>The unique message identifier, or <see langword="null"/> if not set.</summary>
    public string? MessageId { get; init; }
    /// <summary>The request identifier correlating a reply to its request, or <see langword="null"/> if not set.</summary>
    public string? RequestId { get; init; }
    /// <summary>The business correlation identifier, or <see langword="null"/> if not set.</summary>
    public string? CorrelationId { get; init; }
    /// <summary>The conversation identifier grouping related messages, or <see langword="null"/> if not set.</summary>
    public string? ConversationId { get; init; }
    /// <summary>The identifier of the message that initiated this chain, or <see langword="null"/> if not set.</summary>
    public string? InitiatorId { get; init; }
    /// <summary>The address of the endpoint that produced the message, or <see langword="null"/> if unknown.</summary>
    public Uri? SourceAddress { get; init; }
    /// <summary>The intended destination address, or <see langword="null"/> if not set.</summary>
    public Uri? DestinationAddress { get; init; }
    /// <summary>The address where replies should be sent, or <see langword="null"/> if none.</summary>
    public Uri? ResponseAddress { get; init; }
    /// <summary>The address where fault notifications should be sent, or <see langword="null"/> for the default.</summary>
    public Uri? FaultAddress { get; init; }
    /// <summary>The routing key the transport used to deliver the message.</summary>
    public string RoutingKey { get; init; } = string.Empty;
    /// <summary>The number of times the message had been retried when the snapshot was taken.</summary>
    public int RetryCount { get; init; }
}
