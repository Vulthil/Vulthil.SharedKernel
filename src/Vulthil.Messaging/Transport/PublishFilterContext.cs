namespace Vulthil.Messaging.Transport;

/// <summary>
/// The context flowing through the publish/send filter pipeline for a single outgoing message. A filter may
/// inspect the message and its resolved metadata, adjust <see cref="Context"/>, short-circuit the pipeline by not
/// invoking <c>next</c> (e.g. to capture the message into a transactional outbox), or let it proceed to the
/// transport terminal.
/// </summary>
public sealed class PublishFilterContext
{
    /// <summary>The message being published or sent.</summary>
    public required object Message { get; init; }

    /// <summary>The runtime type of <see cref="Message"/>.</summary>
    public required Type MessageType { get; init; }

    /// <summary>
    /// The resolved, mutable publish context (message id, correlation id, headers, routing key, addresses). The
    /// message id is already assigned by the time filters run, so a filter can read it for persistence.
    /// </summary>
    public required PublishContext Context { get; init; }

    /// <summary>Whether this is a pub/sub publish or a point-to-point send.</summary>
    public required PublishKind Kind { get; init; }

    /// <summary>For <see cref="PublishKind.Send"/>, the destination endpoint address; otherwise <see langword="null"/>.</summary>
    public Uri? DestinationAddress { get; init; }

    /// <summary>A token to observe for cancellation.</summary>
    public CancellationToken CancellationToken { get; init; }
}
