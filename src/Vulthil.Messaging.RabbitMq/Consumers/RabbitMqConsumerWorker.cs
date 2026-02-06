using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal record MessageContextImplementation(string CorrelationId, IDictionary<string, object?> Headers) : IMessageContext;
internal sealed record MessageContextImplementation<T>(T Message, IMessageContext Raw) : MessageContextImplementation(Raw.CorrelationId, Raw.Headers), IMessageContext<T>;

internal sealed class RabbitMqConsumerWorker(
    IServiceScopeFactory serviceScopeFactory,
    QueueDefinition queue,
    IChannel workerChannel,
    MessageTypeCache messageTypeCache,
    JsonSerializerOptions jsonSerializerOptions) : IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly QueueDefinition _queueDefinition = queue;
    private readonly IChannel _channel = workerChannel;
    private readonly MessageTypeCache _typeCache = messageTypeCache;
    private readonly JsonSerializerOptions _jsonOptions = jsonSerializerOptions;

    private string? _consumerTag;

    public async Task StartAsync(CancellationToken ct)
    {
        // 1. Configure QoS for this worker's channel
        await _channel.BasicQosAsync(0, _queueDefinition.PrefetchCount, false, ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        // 2. Attach the handler
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        // 3. Begin consuming
        _consumerTag = await _channel.BasicConsumeAsync(
            queue: _queueDefinition.Name,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            await HandleMessageAsync(ea);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception)
        {
            // Simple requeue logic
            await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var messageTypeName = ea.BasicProperties.Type ?? ea.Exchange;
        var plan = _typeCache.GetPlan(messageTypeName);

        if (plan == null)
        {
            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = new MessageContextImplementation(ea.BasicProperties.CorrelationId ?? string.Empty, ea.BasicProperties.Headers ?? new Dictionary<string, object?>());
        var message = JsonSerializer.Deserialize(ea.Body.Span, plan.MessageType.Type, _jsonOptions);

        foreach (var handler in plan.StandardHandlers)
        {
            await handler(scope.ServiceProvider, message!, context, ea.CancellationToken);
        }

        if (plan.RpcHandler != null)
        {
            object response = await plan.RpcHandler(scope.ServiceProvider, message!, context, ea.CancellationToken);
            await SendResponseAsync(ea, response);
        }
    }

    private async Task SendResponseAsync(BasicDeliverEventArgs ea, object response)
    {
        // SEND RESPONSE
        if (!string.IsNullOrEmpty(ea.BasicProperties.ReplyTo))
        {
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, _jsonOptions);
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.CorrelationId,
                Type = response.GetType().FullName
            };

            await _channel.BasicPublishAsync(string.Empty, ea.BasicProperties.ReplyTo, true, replyProps, responseBytes);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_consumerTag))
        {
            await _channel.BasicCancelAsync(_consumerTag);
        }
        await _channel.DisposeAsync();
    }
}
