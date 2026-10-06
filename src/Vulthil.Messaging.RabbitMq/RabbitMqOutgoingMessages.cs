using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Builds every message the RabbitMQ transport writes to the broker, so the wire rules live in one place. It does no
/// I/O: the producers publish the result and handle failures in their own way. The rules:
/// <list type="bullet">
/// <item><description>A message the transport creates carries its message type as <c>Type</c>, a <c>MessageId</c>,
/// the JSON <c>ContentType</c> and a <c>Timestamp</c>. A retry re-publish keeps the properties of the delivery it
/// re-publishes.</description></item>
/// <item><description>A publish or a request is routed by the context's routing key, else the message type's
/// routing-key formatter, else the empty key. A send goes to its queue through the broker's default
/// exchange.</description></item>
/// <item><description>Publishes and sends are persistent. Requests and replies are not, because they live only as
/// long as the requester waits, and neither are faults, which are a best-effort broadcast.</description></item>
/// <item><description>A message that must reach a queue is mandatory, so the broker returns it when no queue takes
/// it: sends, retry re-publishes and replies. Publishes, requests and faults go to exchanges, where no bound queue is
/// normal.</description></item>
/// </list>
/// </summary>
internal static class RabbitMqOutgoingMessages
{
    /// <summary>
    /// Resolves the correlation id, message id and URN of a message a producer creates: an explicit value on the
    /// context wins, then the message type's configured formatter (correlation id only), then a fresh id.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message.</param>
    /// <param name="context">The producer's context.</param>
    /// <param name="messageConfiguration">The message type's configuration.</param>
    /// <returns>The resolved identifiers.</returns>
    public static RabbitMqMessageIds ResolveIds<TMessage>(TMessage message, PublishContext context, MessageConfiguration messageConfiguration)
        where TMessage : notnull
    {
        var correlationId = context.CorrelationId
            ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
            ?? NewMessageId();

        var messageId = context.MessageId ?? NewMessageId();
        var urn = messageConfiguration.Urn;

        return new RabbitMqMessageIds(correlationId, messageId, urn, urn.AbsoluteUri);
    }

    /// <summary>
    /// Builds a publish to the message type's exchange.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="context">The publish context.</param>
    /// <param name="messageConfiguration">The message type's configuration.</param>
    /// <param name="jsonOptions">The serializer options for the envelope.</param>
    /// <returns>The publish, with its identifiers and activity data.</returns>
    public static RabbitMqProducedMessage Publish<TMessage>(
        TMessage message,
        PublishContext context,
        MessageConfiguration messageConfiguration,
        JsonSerializerOptions jsonOptions)
        where TMessage : notnull
    {
        var ids = ResolveIds(message, context, messageConfiguration);
        var exchange = messageConfiguration.Exchange;

        var properties = CreateProperties(ids.UrnString, ids.MessageId, context.Headers);
        properties.ReplyTo = RabbitMqAddress.ResolveRoutingKey(context.ResponseAddress);
        properties.CorrelationId = ids.CorrelationId;
        properties.Persistent = true;

        var body = SerializeEnvelope(message, context, ids, jsonOptions, requestId: null);
        var publish = new RabbitMqOutgoingMessage(exchange, ResolveRoutingKey(message, context, messageConfiguration), Mandatory: false, properties, body);
        return new RabbitMqProducedMessage(publish, ids, "publish", exchange);
    }

    /// <summary>
    /// Builds a point-to-point send to <paramref name="queueName"/> through the broker's default exchange. The message
    /// type's correlation id formatter still applies; its exchange and routing-key formatter do not, because the
    /// destination queue is authoritative.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="context">The send context.</param>
    /// <param name="messageConfiguration">The message type's configuration.</param>
    /// <param name="queueName">The destination queue.</param>
    /// <param name="jsonOptions">The serializer options for the envelope.</param>
    /// <returns>The send, with its identifiers and activity data.</returns>
    public static RabbitMqProducedMessage Send<TMessage>(
        TMessage message,
        PublishContext context,
        MessageConfiguration messageConfiguration,
        string queueName,
        JsonSerializerOptions jsonOptions)
        where TMessage : notnull
    {
        var ids = ResolveIds(message, context, messageConfiguration);

        var properties = CreateProperties(ids.UrnString, ids.MessageId, context.Headers);
        properties.ReplyTo = RabbitMqAddress.ResolveRoutingKey(context.ResponseAddress);
        properties.CorrelationId = ids.CorrelationId;
        properties.Persistent = true;

        var body = SerializeEnvelope(message, context, ids, jsonOptions, requestId: null);
        var send = new RabbitMqOutgoingMessage(string.Empty, queueName, Mandatory: true, properties, body);
        return new RabbitMqProducedMessage(send, ids, "send", queueName);
    }

    /// <summary>
    /// Builds a request to the message type's exchange. Its AMQP <c>CorrelationId</c> is <paramref name="requestId"/>,
    /// the slot the reply echoes, and the envelope carries it as the request id, so the business correlation id stays
    /// free and two requests that share a business key never collide on the requester's waiter. The reply goes to the
    /// context's response address, else <paramref name="replyQueue"/>. The request expires after
    /// <paramref name="timeout"/>, so an unanswered request does not outlive its requester, unless the requester waits
    /// indefinitely: then it must not expire before a responder takes it.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="message">The request.</param>
    /// <param name="context">The request context.</param>
    /// <param name="messageConfiguration">The request type's configuration.</param>
    /// <param name="requestId">The dedicated id that correlates the reply with this request.</param>
    /// <param name="replyQueue">The requester's reply queue.</param>
    /// <param name="timeout">How long the requester waits for the reply.</param>
    /// <param name="jsonOptions">The serializer options for the envelope.</param>
    /// <returns>The request, with its identifiers and activity data.</returns>
    public static RabbitMqProducedMessage Request<TRequest>(
        TRequest message,
        RequestContext context,
        MessageConfiguration messageConfiguration,
        string requestId,
        string replyQueue,
        TimeSpan timeout,
        JsonSerializerOptions jsonOptions)
        where TRequest : notnull
    {
        var ids = ResolveIds(message, context, messageConfiguration);
        var exchange = messageConfiguration.Exchange;

        var properties = CreateProperties(ids.UrnString, ids.MessageId, context.Headers);
        properties.CorrelationId = requestId;
        properties.ReplyTo = RabbitMqAddress.ResolveRoutingKey(context.ResponseAddress) ?? replyQueue;
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            properties.Expiration = RabbitMqConstants.FormatExpiration(timeout);
        }

        var body = SerializeEnvelope(message, context, ids, jsonOptions, requestId);
        var request = new RabbitMqOutgoingMessage(exchange, ResolveRoutingKey(message, context, messageConfiguration), Mandatory: false, properties, body);
        return new RabbitMqProducedMessage(request, ids, "request", exchange);
    }

    /// <summary>
    /// Builds the re-publish of a failed delivery to its queue's retry exchange: the delivery's own properties and
    /// body, with the round the redelivery starts at and the handlers it must run in the headers, and the redelivery
    /// delay as the per-message TTL, after which the retry queue dead-letters the message back to the queue.
    /// </summary>
    /// <param name="delivery">The delivery to re-publish.</param>
    /// <param name="queueName">The queue the delivery came from.</param>
    /// <param name="retryCount">The round the redelivery starts at.</param>
    /// <param name="handlerIdentities">The identities of the handlers the redelivery must run.</param>
    /// <param name="delay">How long the message waits in the retry queue.</param>
    /// <returns>The re-publish.</returns>
    public static RabbitMqOutgoingMessage Retry(
        BasicDeliverEventArgs delivery,
        string queueName,
        int retryCount,
        IEnumerable<string> handlerIdentities,
        TimeSpan delay)
    {
        var headers = CopyHeaders(delivery);
        headers[RabbitMqConstants.RetryCountHeader] = retryCount;
        headers[RabbitMqConstants.RetryHandlersHeader] = RabbitMqConstants.SerializeRetryHandlerIdentities(handlerIdentities);
        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Headers = headers,
            Expiration = RabbitMqConstants.FormatExpiration(delay),
        };

        return new RabbitMqOutgoingMessage($"{queueName}.Retry", delivery.RoutingKey, Mandatory: true, properties, delivery.Body);
    }

    /// <summary>
    /// Builds the fault of a consumer that failed for good, routed by <see cref="ResolveFaultRoute"/> from the fault
    /// address of the fault's original context: the address the consumer saw, which an envelope-wrapped delivery
    /// takes from its envelope. The fault's <c>Message</c> is the payload as delivered — the envelope's message for an
    /// envelope-wrapped delivery, otherwise the whole body — because re-serializing the consumer's message type would
    /// drop the fields that a polymorphic registration's interface does not declare. Its <c>Type</c> is
    /// <c>Fault&lt;…&gt;</c> of the delivered type, the name the receiving side resolves faults by.
    /// </summary>
    /// <typeparam name="TMessage">The consumer's message type.</typeparam>
    /// <param name="fault">The fault the dispatcher produced.</param>
    /// <param name="envelopeMessage">The delivered envelope's message, or <see langword="null"/> for a bare delivery.</param>
    /// <param name="delivery">The faulted delivery.</param>
    /// <param name="faultExchangeName">The shared fault exchange.</param>
    /// <param name="messageTypeName">The faulted message's URN, the routing key on the fault exchange.</param>
    /// <param name="jsonOptions">The serializer options for the fault.</param>
    /// <returns>The fault message.</returns>
    public static RabbitMqOutgoingMessage Fault<TMessage>(
        Fault<TMessage> fault,
        JsonElement? envelopeMessage,
        BasicDeliverEventArgs delivery,
        string faultExchangeName,
        string messageTypeName,
        JsonSerializerOptions jsonOptions)
        where TMessage : notnull
    {
        var deliveredFault = new Fault<JsonElement>
        {
            Message = envelopeMessage ?? JsonSerializer.Deserialize<JsonElement>(delivery.Body.Span, jsonOptions),
            ExceptionMessage = fault.ExceptionMessage,
            StackTrace = fault.StackTrace,
            ExceptionType = fault.ExceptionType,
            FaultedAt = fault.FaultedAt,
            OriginalContext = fault.OriginalContext,
        };

        var properties = CreateProperties($"Fault<{delivery.BasicProperties.Type}>", NewMessageId(), headers: null);
        properties.CorrelationId = delivery.BasicProperties.CorrelationId;

        var (exchange, routingKey) = ResolveFaultRoute(fault.OriginalContext.FaultAddress, faultExchangeName, messageTypeName);
        return new RabbitMqOutgoingMessage(exchange, routingKey, Mandatory: false, properties, JsonSerializer.SerializeToUtf8Bytes(deliveredFault, jsonOptions));
    }

    /// <summary>
    /// Builds a request consumer's reply to the delivery's <c>ReplyTo</c> queue through the broker's default exchange,
    /// under the request's AMQP correlation id, which the requester matches the reply by. Returns
    /// <see langword="null"/> for a request without a <c>ReplyTo</c>, which gets no reply.
    /// </summary>
    /// <param name="reply">The reply envelope.</param>
    /// <param name="delivery">The request delivery.</param>
    /// <param name="jsonOptions">The serializer options for the reply.</param>
    /// <returns>The reply, or <see langword="null"/>.</returns>
    public static RabbitMqOutgoingMessage? Reply(MessageEnvelope reply, BasicDeliverEventArgs delivery, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrEmpty(delivery.BasicProperties.ReplyTo))
        {
            return null;
        }

        var properties = CreateProperties(reply.MessageType.AbsoluteUri, reply.MessageId ?? NewMessageId(), headers: null);
        properties.CorrelationId = delivery.BasicProperties.CorrelationId;

        return new RabbitMqOutgoingMessage(string.Empty, delivery.BasicProperties.ReplyTo, Mandatory: true, properties, JsonSerializer.SerializeToUtf8Bytes(reply, jsonOptions));
    }

    /// <summary>
    /// Resolves the broker route for a fault. A faulted delivery with a fault address routes point-to-point through
    /// the broker's default exchange (empty exchange, the address's queue name as the routing key); otherwise the
    /// fault is published by convention to <paramref name="faultExchangeName"/> with the faulted message's URN
    /// (<paramref name="messageTypeName"/>) as the routing key.
    /// </summary>
    /// <param name="faultAddress">The faulted delivery's fault address, or <see langword="null"/> when it has none.</param>
    /// <param name="faultExchangeName">The shared fault exchange.</param>
    /// <param name="messageTypeName">The faulted message's URN.</param>
    /// <returns>The exchange and routing key of the fault.</returns>
    public static (string Exchange, string RoutingKey) ResolveFaultRoute(
        Uri? faultAddress,
        string faultExchangeName,
        string messageTypeName)
        => faultAddress is null
            ? (faultExchangeName, messageTypeName)
            : (string.Empty, RabbitMqAddress.ResolveRoutingKey(faultAddress) ?? string.Empty);

    /// <summary>
    /// Copies the delivery's headers into a dictionary of their own. AMQP properties copied from a delivery share its
    /// header dictionary, so a header set on the copy would otherwise change the delivery itself.
    /// </summary>
    /// <param name="delivery">The delivery whose headers to copy.</param>
    /// <returns>A new dictionary with the delivery's headers.</returns>
    public static Dictionary<string, object?> CopyHeaders(BasicDeliverEventArgs delivery)
        => delivery.BasicProperties.Headers is { } headers ? new(headers) : [];

    private static string ResolveRoutingKey<TMessage>(TMessage message, PublishContext context, MessageConfiguration messageConfiguration)
        where TMessage : notnull
        => context.RoutingKey
            ?? messageConfiguration.RoutingKeyFormatter?.Invoke(message)
            ?? string.Empty;

    private static BasicProperties CreateProperties(string type, string messageId, IReadOnlyDictionary<string, object?>? headers) => new()
    {
        Type = type,
        MessageId = messageId,
        ContentType = RabbitMqConstants.ContentType,
        Headers = headers is null ? null : new Dictionary<string, object?>(headers),
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
    };

    private static byte[] SerializeEnvelope<TMessage>(TMessage message, PublishContext context, RabbitMqMessageIds ids, JsonSerializerOptions jsonOptions, string? requestId)
        where TMessage : notnull
    {
        var envelope = MessageEnvelopeFactory.Create(message, context, ids.MessageId, ids.CorrelationId, ids.Urn, jsonOptions, requestId);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, jsonOptions);
    }

    private static string NewMessageId() => Guid.CreateVersion7().ToString();
}
