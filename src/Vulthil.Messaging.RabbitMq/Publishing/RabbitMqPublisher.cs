using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqPublisher : ITransportPublisher, IInternalPublisher
{
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly RabbitMqChannelPool _channelPool;

    private readonly ConcurrentDictionary<string, bool> _knownExchanges = new();

    public RabbitMqPublisher(
        IMessageConfigurationProvider messageConfigurationProvider,
        RabbitMqChannelPool channelPool,
        ILogger<RabbitMqPublisher> logger)
    {
        _messageConfigurationProvider = messageConfigurationProvider;
        _channelPool = channelPool;
        _logger = logger;
    }

    public async Task InternalPublishAsync<TMessage>(
        byte[] body,
        BasicProperties props,
        string routingKey,
        MessageConfiguration messageConfiguration,
        CancellationToken cancellationToken)
    {
        var exchange = messageConfiguration.Exchange;

        // Publisher confirmations (with tracking) make the awaited BasicPublishAsync wait for the broker ack and
        // throw on a nack or unroutable-mandatory return. The channel pool bounds concurrent publishes; each
        // leased channel is used non-concurrently and reused on return, replacing the single-channel bottleneck.
        var channel = await _channelPool.LeaseAsync(cancellationToken);
        try
        {
            await EnsureExchangeTopologyAsync(channel, exchange, messageConfiguration, cancellationToken);

            // Publish is pub/sub over a fanout/topic exchange: zero bound subscribers is normal, so the message
            // is not mandatory. Broker confirms still apply (a nack throws), guarding against broker-side loss.
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, props, body, cancellationToken);
            _channelPool.Return(channel);
        }
        catch
        {
            await _channelPool.DiscardAsync(channel);
            throw;
        }
    }

    public async Task InternalSendAsync(
        byte[] body,
        BasicProperties props,
        string queueName,
        CancellationToken cancellationToken)
    {
        // Sends route via the broker's default exchange (always exists, always routes by queue name).
        // The destination queue is owned by the receiving service, so we do not declare it here.
        // A send is point-to-point, so a missing destination queue is a real error: publish mandatory so
        // the broker returns an unroutable message and the awaited confirm throws PublishReturnException.
        var channel = await _channelPool.LeaseAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, mandatory: true, props, body, cancellationToken);
            _channelPool.Return(channel);
        }
        catch
        {
            await _channelPool.DiscardAsync(channel);
            throw;
        }
    }

    public async Task PublishAsync<TMessage>(
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

        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var routingKey = publishContext.RoutingKey
           ?? messageConfiguration.RoutingKeyFormatter?.Invoke(message)
           ?? string.Empty;

        var correlationId = publishContext.CorrelationId
            ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
            ?? Guid.CreateVersion7().ToString();

        var messageId = publishContext.MessageId ?? Guid.CreateVersion7().ToString();
        var exchange = messageConfiguration.Exchange;
        var urn = messageConfiguration.Urn;
        var urnString = urn.AbsoluteUri;

        using var activity = MessagingInstrumentation.ActivitySource.StartActivity(
            $"{exchange} publish",
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, "publish");
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, exchange);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, routingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, urnString);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, messageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, correlationId);
        }

        var properties = new BasicProperties()
        {
            Type = urnString,
            MessageId = messageId,
            ReplyTo = RabbitMqAddress.ResolveRoutingKey(publishContext.ResponseAddress),
            CorrelationId = correlationId,
            ContentType = RabbitMqConstants.ContentType,
            Headers = new Dictionary<string, object?>(publishContext.Headers),
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        var envelope = MessageEnvelopeFactory.Create(
            message, publishContext, messageId, correlationId, urn, _messageConfigurationProvider.JsonSerializerOptions);
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, _messageConfigurationProvider.JsonSerializerOptions);

        MessagingLog.Publishing(_logger, urnString, exchange, routingKey, messageId);

        try
        {
            await InternalPublishAsync<TMessage>(body, properties, routingKey, messageConfiguration, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private async ValueTask EnsureExchangeTopologyAsync(IChannel channel, string exchange, MessageConfiguration messageConfiguration, CancellationToken cancellationToken)
    {
        if (_knownExchanges.ContainsKey(exchange))
        {
            return;
        }

        // ExchangeDeclare is idempotent, so a concurrent first-publish burst that declares the same exchange on
        // several pooled channels is harmless; the cache then short-circuits subsequent publishes.
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: messageConfiguration.ExchangeType.ToRabbitExchangeType(),
            durable: messageConfiguration.Durable,
            autoDelete: messageConfiguration.AutoDelete,
            arguments: messageConfiguration.Arguments,
            cancellationToken: cancellationToken);

        _knownExchanges.TryAdd(exchange, true);
        MessagingLog.ExchangeDeclared(_logger, exchange, messageConfiguration.ExchangeType);
    }
}
