namespace Vulthil.Messaging.Abstractions.Consumers;

public interface IConsumer;
public interface IConsumer<in TMessage> : IConsumer
{
    Task ConsumeAsync(IMessageContext<TMessage> messageContext, CancellationToken cancellationToken = default);
}

public interface IMessageContext
{
    // --- Identity & Correlation ---
    string? MessageId { get; }
    string? RequestId { get; }
    string? ConversationId { get; }
    string? CorrelationId { get; }
    string? InitiatorId { get; } // Who started this chain?

    // --- Addressing ---
    Uri? SourceAddress { get; }
    Uri? DestinationAddress { get; }
    Uri? ResponseAddress { get; } // Where should I send the reply?
    Uri? FaultAddress { get; }    // Where should I send errors?

    // --- Transport Details ---
    string RoutingKey { get; }
    IDictionary<string, object?> Headers { get; }

    // --- Timing & Lifecycle ---
    DateTimeOffset? SentTime { get; }
    DateTimeOffset? ExpirationTime { get; }

    // --- Retry Metadata ---
    int RetryCount { get; }       // Current attempt number (0 = first run)
    bool Redelivered { get; }     // True if broker re-sent this (e.g. consumer crashed)
}
public interface IMessageContext<out TMessage> : IMessageContext
{
    TMessage Message { get; }
}
public record Fault<TMessage> where TMessage : notnull
{
    public required TMessage Message { get; init; }
    public required string ExceptionMessage { get; init; }
    public required string? StackTrace { get; init; }
    public required string ExceptionType { get; init; }
    public required DateTimeOffset FaultedAt { get; init; }
    public required IMessageContext OriginalContext { get; init; }
}
