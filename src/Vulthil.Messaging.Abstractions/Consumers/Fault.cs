namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Represents a fault envelope containing the original message and failure details.
/// </summary>
/// <typeparam name="TMessage">The type of the original message.</typeparam>
public record Fault<TMessage> where TMessage : notnull
{
    /// <summary>
    /// Gets the original message that caused the fault.
    /// </summary>
    public required TMessage Message { get; init; }
    /// <summary>
    /// Gets the exception message describing the failure.
    /// </summary>
    public required string ExceptionMessage { get; init; }
    /// <summary>
    /// Gets the stack trace of the exception, or <see langword="null"/> if unavailable.
    /// </summary>
    public required string? StackTrace { get; init; }
    /// <summary>
    /// Gets the fully-qualified type name of the exception.
    /// </summary>
    public required string ExceptionType { get; init; }
    /// <summary>
    /// Gets the UTC timestamp when the fault occurred.
    /// </summary>
    public required DateTimeOffset FaultedAt { get; init; }
    /// <summary>
    /// Gets the original message context at the time of the fault.
    /// </summary>
    public required IMessageContext OriginalContext { get; init; }
}
