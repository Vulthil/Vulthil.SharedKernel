using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Copies the resolved values of a <see cref="PublishContext"/> (already run through the publish pipeline) onto the
/// fresh context the transport terminal builds, so the transport observes the message id, correlation id, routing
/// key, and headers (including the reserved/promoted address keys, which are stored as headers) decided upstream.
/// </summary>
internal static class PublishContextCopier
{
    public static void CopyResolved(PublishContext source, IPublishContext target)
    {
        foreach (var (key, value) in source.Headers)
        {
            target.AddHeader(key, value);
        }

        if (source.RoutingKey is { } routingKey)
        {
            target.SetRoutingKey(routingKey);
        }

        if (source.CorrelationId is { } correlationId)
        {
            target.SetCorrelationId(correlationId);
        }

        if (source.MessageId is { } messageId)
        {
            target.SetMessageId(messageId);
        }
    }
}
