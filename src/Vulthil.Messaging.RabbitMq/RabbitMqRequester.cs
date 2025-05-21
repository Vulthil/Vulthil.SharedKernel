using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq;

internal sealed class RabbitMqRequester : IRequester, IDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<MessageResult>> _callbackMapper = new();

    private readonly IConnection _rabbitMqConnection;
    private readonly TypeCache _typeCache;
    private IChannel? _channel;
    private string? _replyQueueName;
    private readonly SemaphoreSlim _channelSemaphore = new(1, 1);

    public RabbitMqRequester(IConnection rabbitMqConnection, TypeCache typeCache)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _typeCache = typeCache;
    }

    async Task<Result<TResponse>> IRequester.RequestAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("Requester not started. Have any requests been configured?");
        }

        var correlationId = Guid.NewGuid().ToString();

        var basicProperties = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = _replyQueueName,
            ContentType = RabbitMqConstants.ContentType,
            Type = typeof(TRequest).FullName,
        };

        if (!_typeCache.TryGetRequest<TRequest>(out var requestOption))
        {
            throw new ArgumentException($"Request option not found for type {typeof(TRequest).Name}");
        }

        var jsonString = JsonSerializer.Serialize(message, message.GetType());
        var messageBytes = Encoding.UTF8.GetBytes(jsonString);
        var taskCompletionSource = new TaskCompletionSource<MessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _channelSemaphore.WaitAsync(cancellationToken);

        _callbackMapper.TryAdd(correlationId, taskCompletionSource);
        try
        {
            await _channel.BasicPublishAsync(string.Empty, requestOption.RecipientQueueName, true, basicProperties, messageBytes, cancellationToken);
        }
        finally
        {
            _channelSemaphore.Release();
        }
        using var ctr = cancellationToken.Register(() =>
           {
               _callbackMapper.TryRemove(correlationId, out _);
               taskCompletionSource.SetCanceled();
           });

        var result = await taskCompletionSource.Task;

        if (!result.IsSuccess)
        {
            return Result.Failure<TResponse>(Error.Failure("MessageResult.Failure", result.ErrorMessage));
        }
        TResponse? deserializedResult;
        try
        {
            deserializedResult = JsonSerializer.Deserialize<TResponse>(result.Value);
            if (deserializedResult is null)
            {
                return Result.Failure<TResponse>(Error.Failure("MessageResult.Deserialize", "Message deserialization failed."));
            }
        }
        catch
        {
            return Result.Failure<TResponse>(Error.Failure("MessageResult.Deserialize", "Message deserialization failed."));
        }

        return Result.Success(deserializedResult);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _channel ??= await CreateChannelAsync(cancellationToken);

        // declare a server-named queue
        var queueDeclareResult = await _channel.QueueDeclareAsync(cancellationToken: cancellationToken);
        _replyQueueName = queueDeclareResult.QueueName;
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += (model, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId;

            if (string.IsNullOrEmpty(correlationId) || !_callbackMapper.TryRemove(correlationId, out var tcs))
            {
                return Task.CompletedTask;
            }

            var body = ea.Body.ToArray();
            var response = Encoding.UTF8.GetString(body);
            MessageResult? messageResult;
            try
            {
                messageResult = JsonSerializer.Deserialize<MessageResult>(response);
                messageResult ??= MessageResult.Failure("Message deserialization failed.");
            }
            catch
            {
                messageResult = MessageResult.Failure("Message deserialization failed.");
            }

            tcs.TrySetResult(messageResult);

            return Task.CompletedTask;
        };

        await _channel.BasicConsumeAsync(_replyQueueName, true, consumer, cancellationToken: cancellationToken);
    }

    private async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        var channel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.BasicQosAsync(0, 1, false, cancellationToken);
        return channel;
    }

    #region Dispose

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _channel?.Dispose();
            _channelSemaphore.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await (_channel?.DisposeAsync() ?? ValueTask.CompletedTask);
        _channelSemaphore.Dispose();

        Dispose(false);
    }

    #endregion
}
