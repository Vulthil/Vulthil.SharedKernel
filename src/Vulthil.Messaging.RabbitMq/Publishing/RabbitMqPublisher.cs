using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqPublisher : IPublisher, IInternalPublisher, IAsyncDisposable
{
    private readonly IConnection _rabbitMqConnection;
    private readonly MessagingOptions _messagingOptions;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _channelSemaphore = new(1, 1);
    private IChannel? _channel;

    private readonly ConcurrentDictionary<string, bool> _knownExchanges = new();

    public RabbitMqPublisher(IConnection rabbitMqConnection, IOptions<MessagingOptions> messagingOptions)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _messagingOptions = messagingOptions.Value;
        _jsonOptions = _messagingOptions.JsonSerializerOptions;
    }

    public async Task InternalPublishAsync<TMessage>(byte[] body, BasicProperties props, string routingKey, CancellationToken cancellationToken)
    {
        var exchange = typeof(TMessage).FullName!;

        if (_channel == null)
        {
            await _channelSemaphore.WaitAsync(cancellationToken);
            try
            {
                _channel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
            }
            finally
            {
                _channelSemaphore.Release();
            }
        }
        if (!_knownExchanges.ContainsKey(exchange))
        {
            await _channelSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check inside lock to prevent race conditions
                if (!_knownExchanges.ContainsKey(exchange))
                {
                    // Declare the Exchange (Idempotent)
                    // "Topic" is the most flexible default (allows future routing keys)
                    await _channel.ExchangeDeclareAsync(
                        exchange: exchange,
                        type: ExchangeType.Topic,
                        durable: true,
                        autoDelete: false,
                        cancellationToken: cancellationToken);

                    _knownExchanges.TryAdd(exchange, true);
                }
            }
            finally
            {
                _channelSemaphore.Release();
            }
        }

        await _channelSemaphore.WaitAsync(cancellationToken);

        try
        {
            await _channel.BasicPublishAsync(
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

    public async Task PublishAsync<TMessage>(TMessage message, string? routingKey = null, CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);
        var type = message.GetType();

        var finalRoutingKey = routingKey
            ?? RabbitMqConstants.GetMetadata(type, message, _messagingOptions.ReadOnlyRoutingKeyFormatters)
            ?? string.Empty;

        var correlationId = RabbitMqConstants.GetMetadata(type, message, _messagingOptions.ReadOnlyCorrelationIdFormatters)
            ?? Guid.CreateVersion7().ToString();

        var properties = new BasicProperties()
        {
            Type = type.FullName,
            CorrelationId = correlationId,
            ContentType = RabbitMqConstants.ContentType,
            Headers = new Dictionary<string, object?>(),
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);

        await InternalPublishAsync<TMessage>(body, properties, finalRoutingKey, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.DisposeAsync();
        }

        _channelSemaphore.Dispose();

        GC.SuppressFinalize(this);
    }
}
