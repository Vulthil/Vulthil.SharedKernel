using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Envelope;
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
    private readonly bool _partitioned;
    private readonly ConcurrentDictionary<ulong, Task> _inFlight = new();

    private JsonSerializerOptions _jsonOptions => _messageConfigurationProvider.JsonSerializerOptions;

    private string? _consumerTag;

    public RabbitMqConsumerWorker(
        IServiceScopeFactory serviceScopeFactory,
        QueueDefinition queue,
        IChannel channel,
        MessageTypeCache messageTypeCache,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqConsumerWorker> logger,
        int channelIndex,
        bool partitioned)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _queueDefinition = queue;
        _channel = channel;
        _typeCache = messageTypeCache;
        _messageConfigurationProvider = messageConfigurationProvider;
        _logger = logger;
        _channelIndex = channelIndex;
        _partitioned = partitioned;
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
        if (!_partitioned)
        {
            await ProcessDeliveryAsync(ea);
            return;
        }

        // Partitioned queue: dispatch is ordered (single channel, dispatch concurrency 1). Assign the
        // delivery to its partition lane in arrival order, then return so the next delivery is laned in
        // order; the actual processing and ack happen on the lane (deferred ack), giving cross-key
        // parallelism bounded by PrefetchCount while preserving per-key order.
        var prepared = await TryPrepareAsync(ea);
        if (prepared is null)
        {
            return;
        }

        Task work;
        if (prepared.Plan.IsPartitioned)
        {
            var key = prepared.Plan.PartitionKeyExtractor!(prepared.Message, ea, prepared.Envelope);
            work = string.IsNullOrEmpty(key)
                ? ProcessPreparedAsync(prepared, ea)
                : prepared.Plan.Partitioner!.RunSequentialAsync(key, () => ProcessPreparedAsync(prepared, ea));
        }
        else
        {
            // A non-partitioned type sharing a partitioned queue still runs off the receive loop so it does
            // not block ordered dispatch of subsequent deliveries.
            work = ProcessPreparedAsync(prepared, ea);
        }

        TrackInFlight(ea.DeliveryTag, work);
    }

    private void TrackInFlight(ulong deliveryTag, Task work)
    {
        _inFlight[deliveryTag] = work;
        _ = work.ContinueWith(
            _ => _inFlight.TryRemove(deliveryTag, out Task? _),
            CancellationToken.None,
            TaskContinuationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    /// <summary>Non-partitioned path: process the delivery inline and settle on this dispatcher invocation.</summary>
    private async Task ProcessDeliveryAsync(BasicDeliverEventArgs ea)
    {
        var messageTypeName = ea.BasicProperties.Type ?? ea.Exchange;
        using var activity = StartReceiveActivity(ea, messageTypeName);

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

    /// <summary>Partitioned path: dispatch the already-prepared delivery's handlers and settle (deferred ack).</summary>
    private async Task ProcessPreparedAsync(PreparedDelivery prepared, BasicDeliverEventArgs ea)
    {
        using var activity = StartReceiveActivity(ea, prepared.DiagnosticTypeName);

        try
        {
            await DispatchHandlersAsync(prepared.Plan, prepared.Message, ea, prepared.Envelope);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            await HandleFailureAsync(ex, ea, prepared.DiagnosticTypeName);
        }
    }

    private Activity? StartReceiveActivity(BasicDeliverEventArgs ea, string messageTypeName)
    {
        var activity = MessagingInstrumentation.ActivitySource.StartActivity(
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

        return activity;
    }

    private async Task HandleFailureAsync(Exception ex, BasicDeliverEventArgs ea, string messageTypeName)
    {
        var plan = _typeCache.GetPlan(messageTypeName);
        var policy = GetPolicy(plan, _queueDefinition);
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

    private static RetryPolicyDefinition? GetPolicy(MessageExecutionPlan? plan, QueueDefinition queue)
    {
        if (plan is not null)
        {
            var registration = queue.Registrations
                .FirstOrDefault(r => r.MessageType.Type == plan.MessageType.Type && r.RetryPolicy is not null);
            if (registration?.RetryPolicy is { } policy)
            {
                return policy;
            }
        }
        return queue.DefaultRetryPolicy;
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var prepared = await TryPrepareAsync(ea);
        if (prepared is null)
        {
            return;
        }

        await DispatchHandlersAsync(prepared.Plan, prepared.Message, ea, prepared.Envelope);
    }

    /// <summary>
    /// Parses the envelope, resolves the execution plan, and deserializes the message. Settles the delivery
    /// itself for terminal cases — acks (drops) when no plan matches, nacks on a poison/undeserializable body —
    /// and returns <see langword="null"/> in those cases. Otherwise returns the prepared delivery for dispatch.
    /// </summary>
    private async Task<PreparedDelivery?> TryPrepareAsync(BasicDeliverEventArgs ea)
    {
        var bareTypeName = ea.BasicProperties.Type ?? ea.Exchange;
        var envelope = TryParseEnvelope(ea.Body, _jsonOptions);

        var plan = envelope is not null
            ? _typeCache.GetPlanByUrn(envelope.MessageType)
            : _typeCache.GetPlan(bareTypeName);

        var diagnosticTypeName = envelope?.MessageType.AbsoluteUri ?? bareTypeName;

        if (plan is null)
        {
            MessagingLog.NoExecutionPlan(_logger, _queueDefinition.Name, diagnosticTypeName, ea.RoutingKey);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
            return null;
        }

        object? message;
        try
        {
            message = envelope is not null
                ? envelope.Message.Deserialize(plan.MessageType.Type, _jsonOptions)
                : JsonSerializer.Deserialize(ea.Body.Span, plan.MessageType.Type, _jsonOptions);
        }
        catch (JsonException jsonEx)
        {
            MessagingLog.PoisonMessage(_logger, jsonEx, _queueDefinition.Name, diagnosticTypeName, ea.RoutingKey);
            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return null;
        }

        if (message is null)
        {
            MessagingLog.PoisonMessage(_logger, new JsonException("Deserializer returned null."), _queueDefinition.Name, diagnosticTypeName, ea.RoutingKey);
            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return null;
        }

        return new PreparedDelivery(plan, message, envelope, diagnosticTypeName);
    }

    private async Task DispatchHandlersAsync(MessageExecutionPlan plan, object message, BasicDeliverEventArgs ea, MessageEnvelope? envelope)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        foreach (var handler in plan.Handlers)
        {
            await handler.DispatchAsync(scope.ServiceProvider, message, ea, envelope, _channel, ea.CancellationToken);
        }
    }

    /// <summary>
    /// Attempts to deserialize the body as a <see cref="MessageEnvelope"/>. Returns <see langword="null"/>
    /// on any parse error or when the body is not envelope-shaped — the caller then takes the bare-JSON path.
    /// </summary>
    private static MessageEnvelope? TryParseEnvelope(ReadOnlyMemory<byte> body, JsonSerializerOptions options)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(body.Span, options);

            if (envelope is null || envelope.MessageType is null || envelope.Message.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }
            return envelope;
        }
        catch (JsonException)
        {
            return null;
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

            // Drain in-flight partitioned work so deferred acks complete before the channel closes; anything
            // still unacked after the timeout is requeued by the broker on channel close.
            var pending = _inFlight.Values.ToArray();
            if (pending.Length > 0)
            {
                await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(30));
            }

            await _channel.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
            // Channel was already disposed by AutoRecovery; safe to ignore on shutdown.
        }
        catch (TimeoutException)
        {
            // Draining took too long; unacked deliveries are requeued when the channel closes.
        }
    }

    private sealed record PreparedDelivery(MessageExecutionPlan Plan, object Message, MessageEnvelope? Envelope, string DiagnosticTypeName);
}
