using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

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
        catch (Exception ex)
        {
            await HandleFailureAsync(ex, ea);
        }
    }

    private async Task HandleFailureAsync(Exception ex, BasicDeliverEventArgs ea)
    {
        var policy = GetPolicy(ea.RoutingKey, _queueDefinition);
        if (policy is null)
        {
            await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            return;
        }

        var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        int currentRetry = RabbitMqConstants.GetRetryCount(headers);

        if (currentRetry < policy.MaxRetryCount && !policy.GetIgnoredExceptionTypes().Contains(ex.GetType()))
        {
            var delay = policy.GetDelay(currentRetry);

            var props = new BasicProperties(ea.BasicProperties);
            props.Headers ??= new Dictionary<string, object?>();
            props.Headers["x-retry-count"] = currentRetry + 1;

            props.Expiration = delay.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

            await _channel.BasicPublishAsync($"{_queueDefinition.Name}.Retry", ea.RoutingKey, true, props, ea.Body);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
            return;
        }

        var faultAddressKey = RabbitMqConstants.GetHeaderString(headers, "FaultAddress");

        if (!string.IsNullOrEmpty(faultAddressKey))
        {
            try
            {
                var originalBody = JsonSerializer.Deserialize<object>(ea.Body.Span, _jsonOptions);

                var fault = new Fault<object>
                {
                    Message = originalBody!,
                    ExceptionMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                    ExceptionType = ex.GetType().FullName ?? "Unknown",
                    FaultedAt = DateTimeOffset.UtcNow,
                    OriginalContext = MessageContext.CreateContext(ea)
                };

                var faultBody = JsonSerializer.SerializeToUtf8Bytes(fault, _jsonOptions);
                var faultProps = new BasicProperties
                {
                    CorrelationId = ea.BasicProperties.CorrelationId,
                    Type = $"Fault<{ea.BasicProperties.Type}>",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                // Publish to a dedicated Fault Exchange (logical routing)
                await _channel.BasicPublishAsync("Fault.Exchange", faultAddressKey, false, faultProps, faultBody);
            }
            catch
            {
                // Log that we couldn't even send the fault, but don't block the Nack
            }
        }

        await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
    }

    private static RetryPolicyDefinition? GetPolicy(string routingKey, QueueDefinition queue)
    {
        var registration = queue.Registrations
            .FirstOrDefault(r => RabbitMqConstants.GetRoutingKey(r) == routingKey);

        return registration?.RetryPolicy
               ?? queue.DefaultRetryPolicy;
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var messageTypeName = ea.BasicProperties.Type ?? ea.Exchange;
        var plan = _typeCache.GetPlan(messageTypeName);

        if (plan == null)
        {
            return;
        }

        object? message = null;
        try
        {
            message = JsonSerializer.Deserialize(ea.Body.Span, plan.MessageType.Type, _jsonOptions);
        }
        catch (JsonException)
        {
            // Poison message: move to Error queue immediately
            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        var routingKey = ea.RoutingKey;
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        foreach (var handlerEntry in plan.StandardHandlers)
        {
            if (handlerEntry.RoutingKey == "#" || handlerEntry.RoutingKey == routingKey)
            {
                await handlerEntry.InvokeAsync(scope.ServiceProvider, message!, ea, ea.CancellationToken);
            }
        }

        if (plan.RpcHandler is not null && (plan.RpcHandler.RoutingKey == "#" || plan.RpcHandler.RoutingKey == routingKey))
        {
            await plan.RpcHandler.InvokeAsync(scope.ServiceProvider, message!, ea, _channel, ea.CancellationToken);
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
