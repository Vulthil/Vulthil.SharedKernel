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
    where TMessage : notnull;
