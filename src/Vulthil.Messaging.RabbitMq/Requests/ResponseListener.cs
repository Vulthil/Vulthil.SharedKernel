using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class ResponseListener(IOptions<MessagingOptions> messagingOptions) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, IResponseWaiter> _waiters = new();
    private readonly JsonSerializerOptions _jsonOptions = messagingOptions.Value.JsonSerializerOptions;

    private IChannel? _channel;
    public string ReplyToQueueName { get; private set; } = string.Empty;

    public void RegisterWaiter<T>(string correlationId, TaskCompletionSource<Result<T>> tcs) where T : notnull
        => _waiters[correlationId] = new ResponseWaiter<T>(tcs, _jsonOptions);

    public void RemoveWaiter(string correlationId) => _waiters.TryRemove(correlationId, out _);

    public async Task InitializeAsync(IConnection connection)
    {
        _channel = await connection.CreateChannelAsync();

        // exclusive: true ensures this queue dies when this app instance shuts down
        var declareResult = await _channel.QueueDeclareAsync(
            queue: $"callback.{Guid.NewGuid():N}",
            durable: false, exclusive: true, autoDelete: true);

        ReplyToQueueName = declareResult.QueueName;

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (s, ea) =>
        {
            if (ea.BasicProperties.CorrelationId != null &&
                _waiters.TryGetValue(ea.BasicProperties.CorrelationId, out var waiter))
            {
                waiter.Complete(ea.Body.Span);
            }
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await _channel.BasicConsumeAsync(ReplyToQueueName, false, consumer);
    }

    public ValueTask DisposeAsync() => _channel?.DisposeAsync() ?? ValueTask.CompletedTask;
}
