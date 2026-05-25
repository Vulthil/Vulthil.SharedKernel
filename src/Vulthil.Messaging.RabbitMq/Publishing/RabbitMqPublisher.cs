using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.RabbitMq.Telemetry;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqPublisher : IPublisher, IInternalPublisher, IAsyncDisposable
{
    private readonly IConnection _rabbitMqConnection;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqPublisher> _logger;

    private readonly SemaphoreSlim _channelSemaphore = new(1, 1);
    private IChannel? _channel;

    private readonly ConcurrentDictionary<string, bool> _knownExchanges = new();

    public RabbitMqPublisher(
        IConnection rabbitMqConnection,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqPublisher> logger)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _messageConfigurationProvider = messageConfigurationProvider;
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

        await EnsureChannelAsync(cancellationToken);
        await EnsureExchangeTopologyAsync(exchange, messageConfiguration, cancellationToken);

        await _channelSemaphore.WaitAsync(cancellationToken);

        try
        {
            await _channel!.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _channelSemaphore.Release();
        }
    }

    public Task PublishAsync<TMessage>(
       TMessage message,
       CancellationToken cancellationToken)
       where TMessage : notnull => PublishAsync(message, null, cancellationToken);

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

        using var activity = MessagingInstrumentation.ActivitySource.StartActivity(
            $"{exchange} publish",
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, "publish");
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, exchange);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, routingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, type.FullName);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, messageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, correlationId);
        }

        var properties = new BasicProperties()
        {
            Type = type.FullName,
            MessageId = messageId,
            ReplyTo = PublishContext.ResolveRoutingKeyFromUri(publishContext.ResponseAddress),
            CorrelationId = correlationId,
            ContentType = RabbitMqConstants.ContentType,
            Headers = publishContext.Headers,
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message, _messageConfigurationProvider.JsonSerializerOptions);

        MessagingLog.Publishing(_logger, type.FullName ?? type.Name, exchange, routingKey, messageId);

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

    private async ValueTask EnsureExchangeTopologyAsync(string exchange, MessageConfiguration messageConfiguration, CancellationToken cancellationToken)
    {
        if (_knownExchanges.ContainsKey(exchange))
        {
            return;
        }

        await _channelSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_knownExchanges.ContainsKey(exchange))
            {
                return;
            }

            await _channel!.ExchangeDeclareAsync(
                exchange: exchange,
                type: messageConfiguration.ExchangeType.ToRabbitExchangeType(),
                durable: messageConfiguration.Durable,
                autoDelete: messageConfiguration.AutoDelete,
                arguments: messageConfiguration.Arguments,
                cancellationToken: cancellationToken);

            _knownExchanges.TryAdd(exchange, true);
            MessagingLog.ExchangeDeclared(_logger, exchange, messageConfiguration.ExchangeType);
        }
        finally
        {
            _channelSemaphore.Release();
        }
    }

    private async ValueTask EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            return;
        }

        await _channelSemaphore.WaitAsync(cancellationToken);

        try
        {
#pragma warning disable CA1508 // Avoid dead conditional code
            if (_channel is not null)
            {
                return;
            }
#pragma warning restore CA1508

            _channel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _channelSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _channelSemaphore.Dispose();

        GC.SuppressFinalize(this);
    }
}
