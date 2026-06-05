using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// Builds the wire <see cref="MessageEnvelope"/> for an outgoing message from an already-configured
/// <see cref="PublishContext"/>, resolving the correlation id and message id the same way the broker transport
/// does (explicit value, then the configured formatter, then a fresh GUID).
/// </summary>
internal static class OutgoingEnvelope
{
    public static MessageEnvelope Build<TMessage>(
        IMessageConfigurationProvider provider,
        TMessage message,
        PublishContext context,
        string? requestId = null)
        where TMessage : notnull
    {
        var configuration = provider.GetMessageConfiguration(message.GetType());
        var correlationId = context.CorrelationId
            ?? configuration.CorrelationIdFormatter?.Invoke(message)
            ?? Guid.CreateVersion7().ToString();
        var messageId = context.MessageId ?? Guid.CreateVersion7().ToString();

        return MessageEnvelopeFactory.Create(
            message, context, messageId, correlationId, configuration.Urn, provider.JsonSerializerOptions, requestId);
    }
}
