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
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

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

    // RabbitMQ channels must not be used concurrently. Concurrent dispatch (and, on a partitioned queue, the
    // lanes completing in parallel) settles deliveries (ack/nack/retry-republish/fault) and publishes RPC
    // replies on this shared channel, so every channel write is serialized through this gate to avoid
    // interleaved frames. Message processing stays parallel; only the brief settle/publish frames are serialized.
    private readonly SemaphoreSlim _channelGate = new(1, 1);
    private readonly GatedPublisher _gatedPublisher;

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
        _gatedPublisher = PublishThroughGateAsync;
    }

    /// <summary>
    /// Serializes a single channel write. RabbitMQ channels must not be used concurrently, and a partitioned
    /// queue's lanes settle in parallel on the shared channel, so every ack/nack/publish goes through here.
    /// </summary>
    private async Task OnChannelAsync(Func<ValueTask> channelOperation)
    {
        await _channelGate.WaitAsync();
        try
        {
            await channelOperation();
        }
        finally
        {
            _channelGate.Release();
        }
    }

    private Task AckAsync(BasicDeliverEventArgs ea) => OnChannelAsync(() => _channel.BasicAckAsync(ea.DeliveryTag, false));

    private Task NackAsync(BasicDeliverEventArgs ea) => OnChannelAsync(() => _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false));

    /// <summary>
    /// Publishes on the shared consumer channel through the channel gate. Handed to handler dispatch as the
    /// <see cref="GatedPublisher"/> so a request/reply response serializes with the acks, nacks and republishes
    /// settling other deliveries on this channel.
    /// </summary>
    private Task PublishThroughGateAsync(string exchange, string routingKey, bool mandatory, BasicProperties basicProperties, ReadOnlyMemory<byte> body)
        => OnChannelAsync(() => _channel.BasicPublishAsync(exchange, routingKey, mandatory, basicProperties, body));

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

    /// <remarks>
    /// On a partitioned queue dispatch is ordered (single channel, dispatch concurrency 1): each delivery is assigned
    /// to its partition lane in arrival order and the handler returns so the next delivery is laned in order, while
    /// processing/retry/ack run on the lane with a deferred ack — giving cross-key parallelism bounded by
    /// <c>PrefetchCount</c> while preserving per-key order. A non-partitioned type sharing a partitioned queue still
    /// runs off the receive loop so it does not block ordered dispatch.
    /// </remarks>
    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var prepared = await TryPrepareAsync(ea);
        if (prepared is null)
        {
            return;
        }

        if (!_partitioned)
        {
            await ProcessAsync(prepared, ea);
            return;
        }

        Task work;
        if (prepared.Plan.IsPartitioned)
        {
            var key = prepared.Plan.PartitionKeyExtractor!(prepared.Message, ea, prepared.Envelope);
            work = string.IsNullOrEmpty(key)
                ? ProcessAsync(prepared, ea)
                : prepared.Plan.Partitioner!.RunSequentialAsync(key, () => ProcessAsync(prepared, ea));
        }
        else
        {
            work = ProcessAsync(prepared, ea);
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

    /// <summary>
    /// Dispatches a prepared delivery and settles it. When the effective retry policy is in-memory — set
    /// explicitly via <c>UseRetry(r =&gt; r.InMemory())</c> or implied by a partitioned queue — the consumer
    /// is retried in-process while the delivery is held (preserving order); otherwise a failure goes through
    /// the delay-queue re-delivery path.
    /// </summary>
    private async Task ProcessAsync(PreparedDelivery prepared, BasicDeliverEventArgs ea)
    {
        using var activity = StartReceiveActivity(ea, prepared.DiagnosticTypeName);
        var policy = GetPolicy(prepared.Plan, _queueDefinition);

        if (policy is not null && (policy.InMemory || _partitioned))
        {
            await ExecuteWithInMemoryRetryAsync(policy, prepared, ea, activity);
            return;
        }

        try
        {
            await DispatchHandlersAsync(prepared.Plan, prepared.Message, ea, prepared.Envelope);
            await AckAsync(ea);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && ea.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            await HandleFailureAsync(ex, ea, prepared.DiagnosticTypeName);
        }
    }

    /// <summary>
    /// Re-invokes the consumer in-process up to the policy's retry count, holding the delivery (and, on a
    /// partitioned queue, its lane) so a later message cannot overtake the one being retried. Each attempt
    /// runs in a fresh scope. On exhaustion a fault is published and the message is nacked for dead-lettering.
    /// </summary>
    private async Task ExecuteWithInMemoryRetryAsync(RetryPolicyDefinition policy, PreparedDelivery prepared, BasicDeliverEventArgs ea, Activity? activity)
    {
        var baseRetryCount = RabbitMqConstants.GetRetryCount(ea.BasicProperties.Headers);
        for (var attempt = 0; attempt <= policy.MaxRetryCount; attempt++)
        {
            var attemptDelivery = attempt == 0 ? ea : WithRetryCount(ea, baseRetryCount + attempt);
            try
            {
                await DispatchHandlersAsync(prepared.Plan, prepared.Message, attemptDelivery, prepared.Envelope);
                await AckAsync(ea);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && ea.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var canRetry = attempt < policy.MaxRetryCount && !policy.GetIgnoredExceptionTypes().Contains(ex.GetType());
                if (!canRetry)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.AddException(ex);
                    MessagingLog.ConsumerFailed(_logger, ex, _queueDefinition.Name, prepared.DiagnosticTypeName, ea.RoutingKey);
                    await PublishFaultAsync(ex, ea, ea.BasicProperties.Headers ?? new Dictionary<string, object?>(), prepared.DiagnosticTypeName);
                    await NackAsync(ea);
                    return;
                }

                var delay = policy.GetDelay(attempt);
                MessagingLog.ConsumerThrew(_logger, ex, _queueDefinition.Name, prepared.DiagnosticTypeName, ea.RoutingKey, attempt, policy.MaxRetryCount);
                MessagingLog.SchedulingRetry(_logger, _queueDefinition.Name, attempt + 1, policy.MaxRetryCount, delay);

                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, ea.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
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
            await PublishFaultAsync(ex, ea, headers, messageTypeName);
            await NackAsync(ea);
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

            await PublishThroughGateAsync($"{_queueDefinition.Name}.Retry", ea.RoutingKey, true, props, ea.Body);
            await AckAsync(ea);
            return;
        }

        MessagingLog.ConsumerFailed(_logger, ex, _queueDefinition.Name, messageTypeName, ea.RoutingKey);
        await PublishFaultAsync(ex, ea, headers, messageTypeName);
        await NackAsync(ea);
    }

    /// <summary>
    /// Publishes a <see cref="Fault{TMessage}"/> for a terminally-failed delivery. When the delivery carries an
    /// explicit <c>FaultAddress</c> the fault is routed point-to-point to that address (via the broker's default
    /// exchange); otherwise it is published by convention to the shared fault exchange with the faulted message's
    /// URN as the routing key. Exactly one fault is emitted per failure. Publishing is best-effort: a failure to
    /// publish the fault is logged and never disrupts settling the original delivery.
    /// </summary>
    private async Task PublishFaultAsync(Exception ex, BasicDeliverEventArgs ea, IDictionary<string, object?> headers, string messageTypeName)
    {
        var (exchange, routingKey) = ResolveFaultRoute(headers, _messageConfigurationProvider.FaultExchangeName, messageTypeName);

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
                OriginalContext = MessageContextFactory.CreateSnapshot(ea)
            };

            var faultBody = JsonSerializer.SerializeToUtf8Bytes(fault, _jsonOptions);
            var faultProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.CorrelationId,
                Type = $"Fault<{ea.BasicProperties.Type}>",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await PublishThroughGateAsync(exchange, routingKey, false, faultProps, faultBody);
        }
        catch (Exception faultEx)
        {
            MessagingLog.FaultPublishFailed(_logger, faultEx, exchange, routingKey);
        }
    }

    /// <summary>
    /// Resolves the broker route for a fault. A delivery carrying an explicit <c>FaultAddress</c> routes
    /// point-to-point through the broker's default exchange (empty exchange, the address's queue name as the
    /// routing key); otherwise the fault is published by convention to <paramref name="faultExchangeName"/> with
    /// the faulted message's URN (<paramref name="messageTypeName"/>) as the routing key.
    /// </summary>
    internal static (string Exchange, string RoutingKey) ResolveFaultRoute(
        IDictionary<string, object?> headers,
        string faultExchangeName,
        string messageTypeName)
    {
        var faultAddress = RabbitMqConstants.GetHeaderUri(headers, "FaultAddress");
        return faultAddress is null
            ? (faultExchangeName, messageTypeName)
            : (string.Empty, RabbitMqAddress.ResolveRoutingKey(faultAddress) ?? string.Empty);
    }

    private static RetryPolicyDefinition? GetPolicy(RabbitMqPlan? plan, QueueDefinition queue)
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

    /// <summary>
    /// Returns a copy of <paramref name="ea"/> whose <c>x-retry-count</c> header is set to
    /// <paramref name="retryCount"/>, so a consumer reading <see cref="IMessageContext.RetryCount"/> sees the
    /// current in-memory attempt. The delivery's AMQP properties are read-only on the receive side, hence the copy.
    /// </summary>
    internal static BasicDeliverEventArgs WithRetryCount(BasicDeliverEventArgs ea, int retryCount)
    {
        var properties = new BasicProperties(ea.BasicProperties);
        properties.Headers ??= new Dictionary<string, object?>();
        properties.Headers["x-retry-count"] = retryCount;
        return new BasicDeliverEventArgs(
            ea.ConsumerTag, ea.DeliveryTag, ea.Redelivered, ea.Exchange, ea.RoutingKey, properties, ea.Body, ea.CancellationToken);
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
            await AckAsync(ea);
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
            await NackAsync(ea);
            return null;
        }

        if (message is null)
        {
            MessagingLog.PoisonMessage(_logger, new JsonException("Deserializer returned null."), _queueDefinition.Name, diagnosticTypeName, ea.RoutingKey);
            await NackAsync(ea);
            return null;
        }

        return new PreparedDelivery(plan, message, envelope, diagnosticTypeName);
    }

    private async Task DispatchHandlersAsync(RabbitMqPlan plan, object message, BasicDeliverEventArgs ea, MessageEnvelope? envelope)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        foreach (var handler in plan.Handlers)
        {
            await handler.DispatchAsync(scope.ServiceProvider, message, ea, envelope, _gatedPublisher, ea.CancellationToken);
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

    /// <remarks>
    /// Cancels the consumer through the channel gate (in-flight lanes may still be settling on the shared channel),
    /// then drains in-flight partitioned work so deferred acks complete before the channel closes. An
    /// <see cref="ObjectDisposedException"/> (the channel was already disposed by AutoRecovery) and a
    /// <see cref="TimeoutException"/> (draining ran long) are both ignored on shutdown — any still-unacked deliveries
    /// are requeued by the broker when the channel closes.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_consumerTag))
            {
                await _channelGate.WaitAsync();
                try
                {
                    await _channel.BasicCancelAsync(_consumerTag);
                }
                finally
                {
                    _channelGate.Release();
                }
            }

            var pending = _inFlight.Values.ToArray();
            if (pending.Length > 0)
            {
                await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(30));
            }

            await _channel.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by AutoRecovery; ignored on shutdown (see remarks).
        }
        catch (TimeoutException)
        {
            // Drain timed out; unacked deliveries are requeued on channel close (see remarks).
        }
        finally
        {
            _channelGate.Dispose();
        }
    }

    private sealed record PreparedDelivery(RabbitMqPlan Plan, object Message, MessageEnvelope? Envelope, string DiagnosticTypeName);
}
