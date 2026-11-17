using System.Collections.Concurrent;
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

    // Never disposed: a first request can still wait for the lock when the listener is disposed, and it must then
    // fail with ObjectDisposedException instead of waiting on a semaphore that nobody releases. A SemaphoreSlim whose
    // AvailableWaitHandle is never read holds no unmanaged resource.
#pragma warning disable CA2213
    private readonly SemaphoreSlim _initLock = new(1, 1);
#pragma warning restore CA2213

    private IChannel? _channel;
    private string _replyToQueueName = string.Empty;
    private bool _disposed;

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
    /// <exception cref="ObjectDisposedException">The listener is disposed and has no reply queue.</exception>
    public async ValueTask<string> GetReplyToQueueNameAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null)
        {
            return _replyToQueueName;
        }

        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        return _replyToQueueName;
    }

    public void RegisterWaiter<T>(string requestId, TaskCompletionSource<Result<T>> tcs) where T : notnull
        => _waiters[requestId] = new ResponseWaiter<T>(tcs, _messageConfigurationProvider);

    public void RemoveWaiter(string requestId) => _waiters.TryRemove(requestId, out _);

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
#pragma warning disable CA1508 // Double-check after acquiring the lock; another thread may have initialized.
            if (_channel is not null)
            {
                return;
            }
#pragma warning restore CA1508

            var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            try
            {
                var declareResult = await channel.QueueDeclareAsync(
                    queue: $"callback.{Guid.NewGuid():N}",
                    durable: false,
                    exclusive: true,
                    autoDelete: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                _replyToQueueName = declareResult.QueueName;

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => OnResponseReceivedAsync(channel, delivery);

                await channel.BasicConsumeAsync(
                    queue: _replyToQueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // The next request starts over on a new channel, so a channel whose setup failed or timed out is never
                // used again; left open, every such request would leave one more open channel on the connection.
                await channel.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _channel = channel;
            MessagingLog.ResponseListenerStarted(_logger, _replyToQueueName);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task OnResponseReceivedAsync(IChannel channel, BasicDeliverEventArgs ea)
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

        // The ack goes to the channel the reply arrived on, not to _channel: disposing the listener clears _channel
        // while a reply can still be handled.
        await channel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
    }

    /// <remarks>
    /// Waits for a first request that is opening the reply channel, then closes that channel. A later first request
    /// throws <see cref="ObjectDisposedException"/> instead of opening a channel that nobody closes.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_channel is { } channel)
                {
                    _channel = null;
                    await channel.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _initLock.Release();
        }

        GC.SuppressFinalize(this);
    }
}
