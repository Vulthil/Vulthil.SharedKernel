using System.Diagnostics;
using System.Text.Json;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Shared wire-message construction for the RabbitMQ producer paths (<c>RabbitMqPublisher</c>,
/// <c>RabbitMqSendEndpoint</c>, <c>RabbitMqRequester</c>): resolving the correlation/message identifiers, the common
/// <see cref="BasicProperties"/> fields, the serialized <see cref="MessageEnvelope"/>, and the producer activity
/// tags. Each producer applies its own routing/exchange selection and the <see cref="BasicProperties"/> fields that
/// legitimately differ per operation (reply-to resolution, the wire correlation id, persistence, expiration) on top
/// of the shared result, so the extraction covers only the parts the three sites compute identically.
/// </summary>
internal static class RabbitMqWireMessageBuilder
{
    /// <summary>The identifiers resolved for a single outgoing message, shared by every producer path.</summary>
    /// <param name="CorrelationId">The resolved business correlation identifier.</param>
    /// <param name="MessageId">The resolved message identifier.</param>
    /// <param name="Urn">The stable wire URN for the message type.</param>
    /// <param name="UrnString">The URN's absolute URI string, used as the AMQP <c>Type</c> and activity tag.</param>
    internal readonly record struct ResolvedIds(string CorrelationId, string MessageId, Uri Urn, string UrnString);

    /// <summary>
    /// Resolves the correlation id, message id, and URN for <paramref name="message"/> the same way across every
    /// producer path: an explicit value on <paramref name="context"/> wins, then the type's configured formatter
    /// (correlation id only), then a fresh id.
    /// </summary>
    public static ResolvedIds ResolveIds<TMessage>(TMessage message, PublishContext context, MessageConfiguration messageConfiguration)
        where TMessage : notnull
    {
        var correlationId = context.CorrelationId
            ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
            ?? Guid.CreateVersion7().ToString();

        var messageId = context.MessageId ?? Guid.CreateVersion7().ToString();
        var urn = messageConfiguration.Urn;

        return new ResolvedIds(correlationId, messageId, urn, urn.AbsoluteUri);
    }

    /// <summary>
    /// Builds and serializes the <see cref="MessageEnvelope"/> for the outgoing message. Callers that convert a
    /// publish failure into a typed result (rather than letting it propagate) must call this from within their own
    /// try/catch, since serialization can fail for the same reasons the send itself can.
    /// </summary>
    public static byte[] SerializeEnvelope<TMessage>(
        TMessage message,
        PublishContext context,
        string messageId,
        string correlationId,
        Uri urn,
        JsonSerializerOptions jsonOptions,
        string? requestId = null)
        where TMessage : notnull
    {
        var envelope = MessageEnvelopeFactory.Create(message, context, messageId, correlationId, urn, jsonOptions, requestId);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, jsonOptions);
    }

    /// <summary>
    /// Creates the <see cref="BasicProperties"/> fields common to every RabbitMQ producer path. Callers set the
    /// remaining fields that legitimately differ per operation (<c>ReplyTo</c>, the wire <c>CorrelationId</c>,
    /// <c>Persistent</c>, <c>Expiration</c>).
    /// </summary>
    public static BasicProperties CreateBaseProperties(string urnString, string messageId, IReadOnlyDictionary<string, object?> headers) => new()
    {
        Type = urnString,
        MessageId = messageId,
        ContentType = RabbitMqConstants.ContentType,
        Headers = new Dictionary<string, object?>(headers),
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
    };

    /// <summary>
    /// Starts a producer <see cref="Activity"/> and applies the standard Vulthil messaging tags. Returns
    /// <see langword="null"/> when no listener is recording the transport's activity source.
    /// </summary>
    public static Activity? StartProducerActivity(
        string name, string operation, string destination, string routingKey, string urnString, string messageId, string correlationId)
    {
        var activity = MessagingInstrumentation.ActivitySource.StartActivity(name, ActivityKind.Producer);
        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, operation);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, destination);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, routingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, urnString);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, messageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, correlationId);
        }

        return activity;
    }
}
