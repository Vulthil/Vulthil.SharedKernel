using System.Collections.Concurrent;
using System.Text.Json;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq.Publishing;


internal sealed class RabbitMqPublisher : IPublisher, IInternalPublisher, IAsyncDisposable
{
    private readonly IConnection _rabbitMqConnection;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;

    private readonly SemaphoreSlim _channelSemaphore = new(1, 1);
    private IChannel? _channel;

    private readonly ConcurrentDictionary<string, bool> _knownExchanges = new();

    public RabbitMqPublisher(IConnection rabbitMqConnection, IMessageConfigurationProvider messageConfigurationProvider)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _messageConfigurationProvider = messageConfigurationProvider;
    }

    public async Task InternalPublishAsync<TMessage>(byte[] body, BasicProperties props, string routingKey, MessageConfiguration messageConfiguration, CancellationToken cancellationToken)
    {
        var type = typeof(TMessage);

        // Prefer explicit exchange from publish definition, otherwise use the CLR type full name
        var exchange = messageConfiguration.Exchange ?? type.FullName!;

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

    public async Task PublishAsync<TMessage>(TMessage message, Func<IPublishContext, ValueTask>? configureContext = null, CancellationToken cancellationToken = default)
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

        var properties = new BasicProperties()
        {
            Type = type.FullName,
            MessageId = publishContext.MessageId ?? Guid.CreateVersion7().ToString(),
            ReplyTo = PublishContext.ResolveRoutingKeyFromUri(publishContext.ResponseAddress), // Map URI back to string
            CorrelationId = correlationId,
            ContentType = RabbitMqConstants.ContentType,
            Headers = publishContext.Headers,
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message, _messageConfigurationProvider.JsonSerializerOptions);

        await InternalPublishAsync<TMessage>(body, properties, routingKey, messageConfiguration, cancellationToken);
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
            // Double-check inside lock to prevent race conditions
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
            // Double-check inside lock to prevent race conditions
            if (_channel is not null)
            {
                return;
            }
#pragma warning restore CA1508 // Avoid dead conditional code

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
