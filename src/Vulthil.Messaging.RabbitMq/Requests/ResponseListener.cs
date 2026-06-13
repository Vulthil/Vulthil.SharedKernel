using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class ResponseListener : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<ResponseListener> _logger;
    private readonly ConcurrentDictionary<string, IResponseWaiter> _waiters = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private JsonSerializerOptions JsonOptions => _messageConfigurationProvider.JsonSerializerOptions;
    private IChannel? _channel;
    private string _replyToQueueName = string.Empty;

    public ResponseListener(
        IConnection connection,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<ResponseListener> logger)
    {
        _connection = connection;
        _messageConfigurationProvider = messageConfigurationProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the name of the exclusive reply-to queue this listener subscribes to.
    /// Awaits initialization on first access if the listener has not started yet.
    /// </summary>
    public async ValueTask<string> GetReplyToQueueNameAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null)
        {
            return _replyToQueueName;
        }

        await EnsureStartedAsync(cancellationToken);
        return _replyToQueueName;
    }

    public void RegisterWaiter<T>(string requestId, TaskCompletionSource<Result<T>> tcs, Uri responseUrn) where T : notnull
        => _waiters[requestId] = new ResponseWaiter<T>(tcs, JsonOptions, responseUrn);

    public void RemoveWaiter(string requestId) => _waiters.TryRemove(requestId, out _);

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
#pragma warning disable CA1508 // Double-check after acquiring the lock; another thread may have initialized.
            if (_channel is not null)
            {
                return;
            }
#pragma warning restore CA1508

            var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            var declareResult = await channel.QueueDeclareAsync(
                queue: $"callback.{Guid.NewGuid():N}",
                durable: false,
                exclusive: true,
                autoDelete: true,
                cancellationToken: cancellationToken);

            _replyToQueueName = declareResult.QueueName;

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += OnResponseReceivedAsync;

            await channel.BasicConsumeAsync(
                queue: _replyToQueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _channel = channel;
            MessagingLog.ResponseListenerStarted(_logger, _replyToQueueName);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task OnResponseReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var correlationId = ea.BasicProperties.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId) && _waiters.TryGetValue(correlationId, out var waiter))
        {
            waiter.Complete(ea.Body.Span);
        }
        else if (!string.IsNullOrEmpty(correlationId))
        {
            MessagingLog.ResponseWaiterNotFound(_logger, correlationId);
        }

        if (_channel is not null)
        {
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
