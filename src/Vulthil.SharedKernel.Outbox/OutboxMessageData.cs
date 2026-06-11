namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Contains the data of an outbox message fetched for delivery.
/// </summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Type">The fully-qualified type name of the message.</param>
/// <param name="Content">The JSON-serialized message content.</param>
/// <param name="TraceParent">Distributed TraceParent</param>
/// <param name="TraceState">Distributed TraceState</param>
/// <param name="Destination">The sink this message is relayed to, used to select the dispatcher.</param>
/// <param name="Metadata">Optional destination-specific metadata (JSON), or <see langword="null"/>.</param>
public readonly record struct OutboxMessageData(Guid Id, string Type, string Content, string? TraceParent, string? TraceState, OutboxDestination Destination, string? Metadata);

/// <summary>
/// Contains failure details for an outbox message that could not be published.
/// </summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Error">The error message describing the failure.</param>
public readonly record struct OutboxMessageFailure(Guid Id, string Error);
