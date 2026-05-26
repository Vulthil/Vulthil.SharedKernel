using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Telemetry;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed class RabbitMqConsumerWorker : IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly QueueDefinition _queueDefinition;
    private readonly IChannel _channel;
    private readonly MessageTypeCache _typeCache;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqConsumerWorker> _logger;
    private readonly int _channelIndex;

    private JsonSerializerOptions _jsonOptions => _messageConfigurationProvider.JsonSerializerOptions;

    private string? _consumerTag;

    public RabbitMqConsumerWorker(
        IServiceScopeFactory serviceScopeFactory,
        QueueDefinition queue,
        IChannel channel,
        MessageTypeCache messageTypeCache,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqConsumerWorker> logger,
        int channelIndex)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _queueDefinition = queue;
        _channel = channel;
        _typeCache = messageTypeCache;
        _messageConfigurationProvider = messageConfigurationProvider;
        _logger = logger;
        _channelIndex = channelIndex;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += OnMessageReceivedAsync;

        _consumerTag = await _channel.BasicConsumeAsync(
            queue: _queueDefinition.Name,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        MessagingLog.WorkerStarted(_logger, _queueDefinition.Name, _channelIndex, _queueDefinition.PrefetchCount, _queueDefinition.ConcurrencyLimit);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var messageTypeName = ea.BasicProperties.Type ?? ea.Exchange;

        using var activity = MessagingInstrumentation.ActivitySource.StartActivity(
            $"{_queueDefinition.Name} receive",
            ActivityKind.Consumer);

        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, "receive");
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, _queueDefinition.Name);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, ea.RoutingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, messageTypeName);
            activity.SetTag(MessagingInstrumentation.Tags.QueueName, _queueDefinition.Name);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, ea.BasicProperties.MessageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, ea.BasicProperties.CorrelationId);
            activity.SetTag(MessagingInstrumentation.Tags.RetryCount, RabbitMqConstants.GetRetryCount(ea.BasicProperties.Headers));
        }

        try
        {
            await HandleMessageAsync(ea);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            await HandleFailureAsync(ex, ea, messageTypeName);
        }
    }

    private async Task HandleFailureAsync(Exception ex, BasicDeliverEventArgs ea, string messageTypeName)
    {
        var policy = GetPolicy(ea.RoutingKey, _queueDefinition);
        var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        int currentRetry = RabbitMqConstants.GetRetryCount(headers);

        if (policy is null)
        {
            MessagingLog.ConsumerFailed(_logger, ex, _queueDefinition.Name, messageTypeName, ea.RoutingKey);
            await PublishFaultIfRequestedAsync(ex, ea, headers);
            await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            return;
        }

        MessagingLog.ConsumerThrew(_logger, ex, _queueDefinition.Name, messageTypeName, ea.RoutingKey, currentRetry, policy.MaxRetryCount);

        if (currentRetry < policy.MaxRetryCount && !policy.GetIgnoredExceptionTypes().Contains(ex.GetType()))
        {
            var delay = policy.GetDelay(currentRetry);
            MessagingLog.SchedulingRetry(_logger, _queueDefinition.Name, currentRetry + 1, policy.MaxRetryCount, delay);

            var props = new BasicProperties(ea.BasicProperties);
            props.Headers ??= new Dictionary<string, object?>();
            props.Headers["x-retry-count"] = currentRetry + 1;

            props.Expiration = delay.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

            await _channel.BasicPublishAsync($"{_queueDefinition.Name}.Retry", ea.RoutingKey, true, props, ea.Body);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
            return;
        }

        MessagingLog.ConsumerFailed(_logger, ex, _queueDefinition.Name, messageTypeName, ea.RoutingKey);
        await PublishFaultIfRequestedAsync(ex, ea, headers);
        await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
    }

    private async Task PublishFaultIfRequestedAsync(Exception ex, BasicDeliverEventArgs ea, IDictionary<string, object?> headers)
    {
        var faultAddressKey = RabbitMqConstants.GetHeaderString(headers, "FaultAddress");
        if (string.IsNullOrEmpty(faultAddressKey))
        {
            return;
        }

        try
        {
            var originalBody = JsonSerializer.Deserialize<JsonElement>(ea.Body.Span, _jsonOptions);

            var fault = new Fault<JsonElement>
            {
                Message = originalBody,
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

            await _channel.BasicPublishAsync(_messageConfigurationProvider.FaultExchangeName, faultAddressKey, false, faultProps, faultBody);
        }
        catch (Exception faultEx)
        {
            MessagingLog.FaultPublishFailed(_logger, faultEx, _messageConfigurationProvider.FaultExchangeName, faultAddressKey);
        }
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
            MessagingLog.NoExecutionPlan(_logger, _queueDefinition.Name, messageTypeName, ea.RoutingKey);
            return;
        }

        object? message;
        try
        {
            message = JsonSerializer.Deserialize(ea.Body.Span, plan.MessageType.Type, _jsonOptions);
        }
        catch (JsonException jsonEx)
        {
            MessagingLog.PoisonMessage(_logger, jsonEx, _queueDefinition.Name, messageTypeName, ea.RoutingKey);
            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        if (message is null)
        {
            MessagingLog.PoisonMessage(_logger, new JsonException("Deserializer returned null."), _queueDefinition.Name, messageTypeName, ea.RoutingKey);
            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        foreach (var handler in plan.Handlers)
        {
            await handler.DispatchAsync(scope.ServiceProvider, message, ea, _channel, ea.CancellationToken);
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
            // Channel was already disposed by AutoRecovery; safe to ignore on shutdown.
        }
    }
}
