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

    /// <remarks>
    /// Publishes pub/sub over the message's fanout/topic exchange. Publisher confirmations (with tracking) make the
    /// awaited <c>BasicPublishAsync</c> wait for the broker ack and throw on a nack. The publish is not mandatory
    /// because zero bound subscribers is normal for pub/sub; broker confirms still guard against broker-side loss.
    /// Channels come from a bounded pool — each leased channel is used non-concurrently and returned for reuse, or
    /// discarded if it faults.
    /// </remarks>
    public async Task InternalPublishAsync<TMessage>(
        byte[] body,
        BasicProperties props,
        string routingKey,
        MessageConfiguration messageConfiguration,
        CancellationToken cancellationToken)
    {
        var exchange = messageConfiguration.Exchange;

        var channel = await _channelPool.LeaseAsync(cancellationToken);
        try
        {
            await EnsureExchangeTopologyAsync(channel, exchange, messageConfiguration, cancellationToken);

            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, props, body, cancellationToken);
            _channelPool.Return(channel);
        }
        catch
        {
            await _channelPool.DiscardAsync(channel);
            throw;
        }
    }

    /// <remarks>
    /// Sends point-to-point via the broker's default exchange, which always routes by queue name; the destination
    /// queue is owned by the receiving service and is not declared here. The publish is mandatory, so a missing
    /// destination queue makes the broker return the message and the awaited confirm throw a <c>PublishReturnException</c>.
    /// </remarks>
    public async Task InternalSendAsync(
        byte[] body,
        BasicProperties props,
        string queueName,
        CancellationToken cancellationToken)
    {
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
