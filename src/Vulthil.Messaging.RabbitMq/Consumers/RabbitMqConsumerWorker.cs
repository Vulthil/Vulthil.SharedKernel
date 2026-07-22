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
        await _channelGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await channelOperation().ConfigureAwait(false);
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
            cancellationToken: cancellationToken).ConfigureAwait(false);

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
        var prepared = await TryPrepareAsync(ea).ConfigureAwait(false);
        if (prepared is null)
        {
            return;
        }

        if (!_partitioned)
        {
            await ProcessAsync(prepared, ea).ConfigureAwait(false);
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
    /// Dispatches a prepared delivery in rounds and settles it. Round zero runs every pending handler (the full
    /// plan, or the failed subset stamped on a delayed-retry re-delivery); each further round re-runs only the
    /// handlers that failed the round before, so a consumer that already succeeded is never re-run. A failed
    /// handler retries per its own effective policy: it is held in-process — preserving order — when the queue
    /// is partitioned or its policy is in-memory, and re-published through the retry queue otherwise. A handler
    /// whose retries are exhausted (or that has no policy) fails terminally: its fault is published immediately,
    /// and the delivery is nacked for dead-lettering when the final round ends with a terminal failure.
    /// </summary>
    private async Task ProcessAsync(PreparedDelivery prepared, BasicDeliverEventArgs ea)
    {
        using var activity = StartReceiveActivity(ea, prepared.DiagnosticTypeName);
        var pending = ResolvePendingHandlers(prepared.Plan, ea);
        if (pending.Count == 0)
        {
            await AckAsync(ea).ConfigureAwait(false);
            return;
        }

        var baseRound = RabbitMqConstants.GetRetryCount(ea.BasicProperties.Headers);
        var round = baseRound;
        while (true)
        {
            var attemptDelivery = round == baseRound ? ea : WithRetryCount(ea, round);
            var failures = await DispatchRoundAsync(pending, prepared, attemptDelivery).ConfigureAwait(false);
            if (failures is null)
            {
                return;
            }

            if (failures.Count == 0)
            {
                await AckAsync(ea).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            var (retryable, terminal) = PartitionFailures(failures, round);
            await PublishTerminalFaultsAsync(terminal, prepared, ea, activity).ConfigureAwait(false);

            if (retryable.Count == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, terminal[^1].Exception.Message);
                await NackAsync(ea).ConfigureAwait(false);
                return;
            }

            var delay = ScheduleRetry(retryable, round, ea.RoutingKey);
            if (!_partitioned && retryable.TrueForAll(static failure => !failure.Policy.InMemory))
            {
                RecordRetryableFailures(retryable, activity);
                await RepublishForRetryAsync(retryable, round, delay, ea).ConfigureAwait(false);
                await AckAsync(ea).ConfigureAwait(false);
                return;
            }

            if (!await TryDelayAsync(delay, ea.CancellationToken).ConfigureAwait(false))
            {
                return;
            }

            pending = retryable.ConvertAll(static failure => failure.Handler);
            round++;
        }
    }

    /// <summary>
    /// Resolves the handlers this delivery dispatches. A first delivery (no retry-handlers header) runs the
    /// full plan; a delayed-retry re-delivery carries the identities of the handlers that failed and runs only
    /// those. Identities that no longer match a registered handler (the consumer was renamed or removed since
    /// the re-publish) are logged and skipped; when none remain the caller acks the delivery without dispatch.
    /// </summary>
    private List<MessageHandler> ResolvePendingHandlers(RabbitMqPlan plan, BasicDeliverEventArgs ea)
    {
        var identities = RabbitMqConstants.GetRetryHandlerIdentities(ea.BasicProperties.Headers);
        if (identities is null)
        {
            return [.. plan.Handlers];
        }

        var requested = new HashSet<string>(identities, StringComparer.Ordinal);
        var pending = plan.Handlers.Where(handler => requested.Contains(handler.Identity)).ToList();

        var missing = requested.Except(pending.Select(static handler => handler.Identity), StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            MessagingLog.RetryHandlersMissing(_logger, _queueDefinition.Name, string.Join(", ", missing));
        }

        return pending;
    }

    /// <summary>
    /// Runs one dispatch round: every handler in <paramref name="pending"/> once, in plan order, sharing one
    /// fresh scope. Per-handler failures are collected instead of aborting the round, so one consumer's
    /// exception cannot skip another consumer, and the caller retries only the handlers that actually failed.
    /// Returns <see langword="null"/> when dispatch was cancelled by shutdown — the delivery is then left
    /// unsettled for broker redelivery.
    /// </summary>
    private async Task<List<HandlerFailure>?> DispatchRoundAsync(List<MessageHandler> pending, PreparedDelivery prepared, BasicDeliverEventArgs ea)
    {
        var failures = new List<HandlerFailure>();
        var scope = _serviceScopeFactory.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);

        foreach (var handler in pending)
        {
            try
            {
                await handler.DispatchAsync(scope.ServiceProvider, prepared.Message, ea, prepared.Envelope, _gatedPublisher, ea.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ea.CancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception exception)
            {
                failures.Add(new HandlerFailure(handler, exception));
            }
        }

        return failures;
    }

    /// <summary>
    /// Splits a round's failures by each handler's own effective retry policy: a failure is retryable while the
    /// current round is below the policy's retry count and the exception is not on the policy's ignore list; a
    /// failure with no policy, an exhausted budget, or an ignored exception is terminal.
    /// </summary>
    private static (List<RetryableFailure> Retryable, List<HandlerFailure> Terminal) PartitionFailures(List<HandlerFailure> failures, int round)
    {
        var retryable = new List<RetryableFailure>();
        var terminal = new List<HandlerFailure>();
        foreach (var failure in failures)
        {
            if (failure.Handler.RetryPolicy is { } policy
                && round < policy.MaxRetryCount
                && !policy.GetIgnoredExceptionTypes().Contains(failure.Exception.GetType()))
            {
                retryable.Add(new RetryableFailure(failure.Handler, failure.Exception, policy));
            }
            else
            {
                terminal.Add(failure);
            }
        }

        return (retryable, terminal);
    }

    /// <summary>
    /// Publishes one fault per terminally-failed handler the moment it exhausts, so a terminal failure is
    /// reported even when other handlers on the same delivery keep retrying (the delivery itself stays live
    /// for them and only dead-letters when the final round ends with a terminal failure).
    /// </summary>
    private async Task PublishTerminalFaultsAsync(List<HandlerFailure> terminal, PreparedDelivery prepared, BasicDeliverEventArgs ea, Activity? activity)
    {
        if (terminal.Count == 0)
        {
            return;
        }

        var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        foreach (var failure in terminal)
        {
            MessagingLog.ConsumerFailed(_logger, failure.Exception, _queueDefinition.Name, failure.Handler.Identity, prepared.DiagnosticTypeName, ea.RoutingKey);
            activity?.AddException(failure.Exception);
            await PublishFaultAsync(failure.Exception, ea, headers, prepared.Envelope, prepared.DiagnosticTypeName).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Logs each retryable failure and computes the round's retry delay: the longest delay requested by any
    /// retrying handler's policy, so no handler is retried earlier than its own back-off asks.
    /// </summary>
    private TimeSpan ScheduleRetry(List<RetryableFailure> retryable, int round, string routingKey)
    {
        var delay = TimeSpan.Zero;
        var maxRetryCount = 0;
        foreach (var failure in retryable)
        {
            MessagingLog.ConsumerThrew(_logger, failure.Exception, _queueDefinition.Name, failure.Handler.Identity, routingKey, round, failure.Policy.MaxRetryCount);

            var handlerDelay = failure.Policy.GetDelay(round);
            delay = handlerDelay > delay ? handlerDelay : delay;
            maxRetryCount = Math.Max(maxRetryCount, failure.Policy.MaxRetryCount);
        }

        MessagingLog.SchedulingRetry(_logger, _queueDefinition.Name, round + 1, maxRetryCount, delay);
        return delay;
    }

    private static void RecordRetryableFailures(List<RetryableFailure> retryable, Activity? activity)
    {
        foreach (var failure in retryable)
        {
            activity?.AddException(failure.Exception);
        }

        activity?.SetStatus(ActivityStatusCode.Error, retryable[^1].Exception.Message);
    }

    /// <summary>
    /// Re-publishes the delivery to the queue's retry queue for delayed re-delivery, stamping the next retry
    /// round and the identities of the handlers that failed so the re-delivery dispatches only those. The
    /// per-message TTL is the round's computed delay; the caller acks the original delivery afterwards.
    /// </summary>
    private async Task RepublishForRetryAsync(List<RetryableFailure> retryable, int round, TimeSpan delay, BasicDeliverEventArgs ea)
    {
        var props = new BasicProperties(ea.BasicProperties);
        props.Headers ??= new Dictionary<string, object?>();
        props.Headers[RabbitMqConstants.RetryCountHeader] = round + 1;
        props.Headers[RabbitMqConstants.RetryHandlersHeader] = RabbitMqConstants.SerializeRetryHandlerIdentities(retryable.Select(static failure => failure.Handler.Identity));
        props.Expiration = delay.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

        await PublishThroughGateAsync($"{_queueDefinition.Name}.Retry", ea.RoutingKey, true, props, ea.Body).ConfigureAwait(false);
    }

    private static async Task<bool> TryDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return true;
        }

        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
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

    /// <summary>
    /// Publishes a <see cref="Fault{TMessage}"/> for a terminally-failed delivery. When the delivery carries an
    /// explicit <c>FaultAddress</c> the fault is routed point-to-point to that address (via the broker's default
    /// exchange); otherwise it is published by convention to the shared fault exchange with the faulted message's
    /// URN as the routing key. Exactly one fault is emitted per failure. The fault's <c>Message</c> is the original
    /// message payload: for an envelope-wrapped delivery the wire envelope is unwrapped (re-parsing the body when
    /// the caller has no parsed envelope at hand), so a subscriber can deserialize the fault as
    /// <see cref="Fault{TMessage}"/> of the faulted message type. Publishing is best-effort: a failure to publish
    /// the fault is logged and never disrupts settling the original delivery.
    /// </summary>
    private async Task PublishFaultAsync(Exception ex, BasicDeliverEventArgs ea, IDictionary<string, object?> headers, MessageEnvelope? envelope, string messageTypeName)
    {
        var (exchange, routingKey) = ResolveFaultRoute(headers, _messageConfigurationProvider.FaultExchangeName, messageTypeName);

        try
        {
            var faultedEnvelope = envelope ?? TryParseEnvelope(ea.Body, _jsonOptions);
            var originalMessage = faultedEnvelope?.Message ?? JsonSerializer.Deserialize<JsonElement>(ea.Body.Span, _jsonOptions);

            var fault = new Fault<JsonElement>
            {
                Message = originalMessage,
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

            await PublishThroughGateAsync(exchange, routingKey, false, faultProps, faultBody).ConfigureAwait(false);
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

    /// <summary>
    /// Returns a copy of <paramref name="ea"/> whose <c>x-retry-count</c> header is set to
    /// <paramref name="retryCount"/>, so a consumer reading <see cref="IMessageContext.RetryCount"/> sees the
    /// current in-memory attempt. The delivery's AMQP properties are read-only on the receive side, hence the copy.
    /// </summary>
    internal static BasicDeliverEventArgs WithRetryCount(BasicDeliverEventArgs ea, int retryCount)
    {
        var properties = new BasicProperties(ea.BasicProperties);
        properties.Headers ??= new Dictionary<string, object?>();
        properties.Headers[RabbitMqConstants.RetryCountHeader] = retryCount;
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
            await AckAsync(ea).ConfigureAwait(false);
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
            await NackAsync(ea).ConfigureAwait(false);
            return null;
        }

        if (message is null)
        {
            MessagingLog.PoisonMessage(_logger, new JsonException("Deserializer returned null."), _queueDefinition.Name, diagnosticTypeName, ea.RoutingKey);
            await NackAsync(ea).ConfigureAwait(false);
            return null;
        }

        return new PreparedDelivery(plan, message, envelope, diagnosticTypeName);
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
                await _channelGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _channel.BasicCancelAsync(_consumerTag).ConfigureAwait(false);
                }
                finally
                {
                    _channelGate.Release();
                }
            }

            var pending = _inFlight.Values.ToArray();
            if (pending.Length > 0)
            {
                await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }

            await _channel.DisposeAsync().ConfigureAwait(false);
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

    private sealed record HandlerFailure(MessageHandler Handler, Exception Exception);

    private sealed record RetryableFailure(MessageHandler Handler, Exception Exception, RetryPolicyDefinition Policy);
}
