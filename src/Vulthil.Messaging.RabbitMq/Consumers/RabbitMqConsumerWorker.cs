using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal record MessageContextImplementation(string CorrelationId, string RoutingKey, IDictionary<string, object?> Headers) : IMessageContext;
internal sealed record MessageContextImplementation<T>(T Message, IMessageContext Raw) : MessageContextImplementation(Raw.CorrelationId, Raw.RoutingKey, Raw.Headers), IMessageContext<T>;

internal sealed class RabbitMqConsumerWorker(
    IServiceScopeFactory serviceScopeFactory,
    QueueDefinition queue,
    IChannel channel,
    MessageTypeCache messageTypeCache,
    JsonSerializerOptions jsonSerializerOptions) : IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly QueueDefinition _queueDefinition = queue;
    private readonly IChannel _channel = channel;
    private readonly MessageTypeCache _typeCache = messageTypeCache;
    private readonly JsonSerializerOptions _jsonOptions = jsonSerializerOptions;

    private string? _consumerTag;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 1. Configure QoS for this worker's channel
        await _channel.BasicQosAsync(0, _queueDefinition.PrefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        // 2. Attach the handler
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        // 3. Begin consuming
        _consumerTag = await _channel.BasicConsumeAsync(
            queue: _queueDefinition.Name,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
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
        var routingKey = ea.RoutingKey ?? string.Empty;
        var context = new MessageContextImplementation(
            ea.BasicProperties.CorrelationId ?? string.Empty,
            routingKey,
            ea.BasicProperties.Headers ?? new Dictionary<string, object?>());
        var message = JsonSerializer.Deserialize(ea.Body.Span, plan.MessageType.Type, _jsonOptions);

        foreach (var handlerEntry in plan.StandardHandlers)
        {
            if (handlerEntry.RoutingKey == "#" || handlerEntry.RoutingKey == routingKey)
            {
                await handlerEntry.Handler(scope.ServiceProvider, message!, context, ea.CancellationToken);
            }
        }

        if (plan.RpcHandler != null && (plan.RpcHandlerRoutingKey == "#" || plan.RpcHandlerRoutingKey == routingKey))
        {
            MessageResult messageResult;
            try
            {
                var response = await plan.RpcHandler.Handler(scope.ServiceProvider, message!, context, ea.CancellationToken);

                var responseJsonString = JsonSerializer.SerializeToUtf8Bytes(response, _jsonOptions);

                messageResult = MessageResult.Success(responseJsonString);
            }
            catch (Exception exception)
            {
                messageResult = MessageResult.Failure(exception.Message);
            }
            await SendResponseAsync(ea, messageResult);
        }
    }

    private async Task SendResponseAsync(BasicDeliverEventArgs ea, MessageResult response)
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
        try
        {
            if (!string.IsNullOrEmpty(_consumerTag))
            {
                await _channel.BasicCancelAsync(_consumerTag);
            }

            await _channel.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
            // Channel was already disposed (e.g., by AutoRecovery mechanism)
        }
    }
}
