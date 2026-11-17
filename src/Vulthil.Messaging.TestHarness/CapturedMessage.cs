using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// A single message captured by the <see cref="ITestHarness"/>: the deserialized payload and the wire
/// <see cref="MessageEnvelope"/> it travelled in (correlation/conversation ids, headers, addresses, …).
/// </summary>
/// <typeparam name="TMessage">The captured message type.</typeparam>
/// <param name="Message">The deserialized message payload.</param>
/// <param name="Envelope">The wire envelope carrying the message metadata.</param>
public sealed record CapturedMessage<TMessage>(TMessage Message, MessageEnvelope Envelope)
    where TMessage : notnull
{
    /// <summary>
    /// Gets the address the message was sent to: the send endpoint's address for a message that
    /// <see cref="ITestHarness.Sent{TMessage}"/> returns, and <see langword="null"/> for a published, consumed, or
    /// requested message. A send does not write its address into the <see cref="Envelope"/>, on the broker or in the
    /// harness, so this property is how a test checks where a message was sent, also when the address names a queue of
    /// another service.
    /// </summary>
    public Uri? DestinationAddress { get; init; }
}
