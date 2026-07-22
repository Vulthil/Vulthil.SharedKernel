using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

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
        await configureContext(publishContext).ConfigureAwait(false);

        var type = message.GetType();
        // MessageConfiguration<T>.CorrelationIdFormatter still applies; Exchange and RoutingKeyFormatter
        // are intentionally ignored on the send path — the destination queue name is authoritative.
        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var ids = RabbitMqWireMessageBuilder.ResolveIds(message, publishContext, messageConfiguration);

        using var activity = RabbitMqWireMessageBuilder.StartProducerActivity(
            $"{_queueName} send", "send", _queueName, _queueName, ids.UrnString, ids.MessageId, ids.CorrelationId);

        var properties = RabbitMqWireMessageBuilder.CreateBaseProperties(ids.UrnString, ids.MessageId, publishContext.Headers);
        properties.ReplyTo = RabbitMqAddress.ResolveRoutingKey(publishContext.ResponseAddress);
        properties.CorrelationId = ids.CorrelationId;
        properties.Persistent = true;

        var body = RabbitMqWireMessageBuilder.SerializeEnvelope(
            message, publishContext, ids.MessageId, ids.CorrelationId, ids.Urn, _messageConfigurationProvider.JsonSerializerOptions);

        MessagingLog.Sending(_logger, ids.UrnString, _queueName, ids.MessageId, ids.CorrelationId);

        try
        {
            await _publisher.InternalSendAsync(body, properties, _queueName, cancellationToken).ConfigureAwait(false);
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
