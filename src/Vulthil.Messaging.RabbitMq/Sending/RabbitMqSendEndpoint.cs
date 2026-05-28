using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Envelope;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.RabbitMq.Telemetry;

namespace Vulthil.Messaging.RabbitMq.Sending;

internal sealed class RabbitMqSendEndpoint : ISendEndpoint
{
    private readonly IInternalPublisher _publisher;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqSendEndpoint> _logger;
    private readonly string _queueName;

    public RabbitMqSendEndpoint(
        Uri address,
        string queueName,
        IInternalPublisher publisher,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqSendEndpoint> logger)
    {
        Address = address;
        _queueName = queueName;
        _publisher = publisher;
        _messageConfigurationProvider = messageConfigurationProvider;
        _logger = logger;
    }

    public Uri Address { get; }

    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => SendAsync(message, null, cancellationToken);

    public async Task SendAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var publishContext = new PublishContext();
        configureContext ??= (_ => ValueTask.CompletedTask);
        await configureContext(publishContext);

        var type = message.GetType();
        // MessageConfiguration<T>.CorrelationIdFormatter still applies; Exchange and RoutingKeyFormatter
        // are intentionally ignored on the send path — the destination queue name is authoritative.
        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var correlationId = publishContext.CorrelationId
            ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
            ?? Guid.CreateVersion7().ToString();
        var messageId = publishContext.MessageId ?? Guid.CreateVersion7().ToString();
        var urn = messageConfiguration.Urn;
        var urnString = urn.AbsoluteUri;

        using var activity = MessagingInstrumentation.ActivitySource.StartActivity(
            $"{_queueName} send",
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, "send");
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, _queueName);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, _queueName);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, urnString);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, messageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, correlationId);
        }

        var properties = new BasicProperties
        {
            Type = urnString,
            MessageId = messageId,
            ReplyTo = PublishContext.ResolveRoutingKeyFromUri(publishContext.ResponseAddress),
            CorrelationId = correlationId,
            ContentType = RabbitMqConstants.ContentType,
            Headers = publishContext.Headers,
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        var envelope = MessageEnvelopeFactory.Create(
            message, publishContext, messageId, correlationId, urn, _messageConfigurationProvider.JsonSerializerOptions);
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, _messageConfigurationProvider.JsonSerializerOptions);

        MessagingLog.Sending(_logger, urnString, _queueName, messageId, correlationId);

        try
        {
            await _publisher.InternalSendAsync(body, properties, _queueName, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
