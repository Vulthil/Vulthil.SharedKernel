using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed class RabbitMqConsumerWorker : IAsyncDisposable
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(30);

    private readonly DeliveryDispatcher _dispatcher;
    private readonly QueueDefinition _queueDefinition;
    private readonly IChannel _channel;
    private readonly QueueDispatchPlans _plans;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqConsumerWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly int _channelIndex;
    private readonly bool _partitioned;
    private readonly ConcurrentDictionary<ulong, Task> _inFlight = new();

    // RabbitMQ channels must not be used concurrently. Concurrent dispatch (and, on a partitioned queue, the
    // lanes completing in parallel) settles deliveries (ack/nack/retry-republish/fault) and publishes RPC
    // replies on this shared channel, so every channel write is serialized through this gate to avoid
    // interleaved frames. Message processing stays parallel; only the brief settle/publish frames are serialized.
    private readonly SemaphoreSlim _channelGate = new(1, 1);

    private JsonSerializerOptions _jsonOptions => _messageConfigurationProvider.JsonSerializerOptions;

    private string? _consumerTag;

    public RabbitMqConsumerWorker(
        DeliveryDispatcher dispatcher,
        QueueDefinition queue,
        IChannel channel,
        QueueDispatchPlans plans,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqConsumerWorker> logger,
        TimeProvider timeProvider,
        int channelIndex)
    {
        _dispatcher = dispatcher;
        _queueDefinition = queue;
        _channel = channel;
        _plans = plans;
        _messageConfigurationProvider = messageConfigurationProvider;
        _logger = logger;
        _timeProvider = timeProvider;
        _channelIndex = channelIndex;
        _partitioned = plans.IsPartitioned;
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
    /// Publishes on the shared consumer channel through the channel gate, so a reply, a fault or a retry
    /// re-publish serializes with the acks and nacks settling other deliveries on this channel.
    /// </summary>
    private Task PublishThroughGateAsync(RabbitMqOutgoingMessage message)
        => OnChannelAsync(() => _channel.BasicPublishAsync(message.Exchange, message.RoutingKey, message.Mandatory, message.Properties, message.Body));

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
        try
        {
            work = StartPartitionedProcessing(prepared, ea);
        }
        catch (Exception exception) when (!ea.CancellationToken.IsCancellationRequested)
        {
            await RejectUnprocessableAsync(ea, prepared.DiagnosticTypeName, exception).ConfigureAwait(false);
            return;
        }

        TrackInFlight(ea.DeliveryTag, work);
    }

    /// <summary>
    /// Starts processing a delivery on a partitioned queue: on its partition's lane when its message type is
    /// partitioned and the delivery has a key, otherwise at once. Reading the key builds a context from the delivery's
    /// metadata, so it throws on metadata the worker cannot parse.
    /// </summary>
    private Task StartPartitionedProcessing(PreparedDelivery prepared, BasicDeliverEventArgs ea)
    {
        if (prepared.Plan.Partition is not { } partition)
        {
            return ProcessAsync(prepared, ea);
        }

        var key = partition.ExtractKey(prepared.Message, ea, prepared.Envelope);
        return string.IsNullOrEmpty(key)
            ? ProcessAsync(prepared, ea)
            : partition.Partitioner.RunSequentialAsync(key, () => ProcessAsync(prepared, ea));
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
    /// Runs a prepared delivery through the <see cref="DeliveryDispatcher"/> and settles it the way the outcome
    /// says: ack it, nack it for dead-lettering, re-publish it through the retry queue and ack it, or leave it
    /// unsettled when shutdown ended the delivery, so the broker delivers it again. A delayed-retry re-delivery
    /// dispatches only the handlers it names. A dispatch that throws instead of returning an outcome — for example on
    /// metadata the worker cannot parse — rejects the delivery (see <see cref="RejectUnprocessableAsync"/>).
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

        DeliveryOutcome outcome;
        try
        {
            using (MessagingLog.BeginDelivery(_logger, _queueDefinition.Name, ea.RoutingKey, prepared.DiagnosticTypeName))
            {
                outcome = await _dispatcher.DispatchAsync(pending, prepared.Message, new DeliveryPort(this, prepared, ea)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (!ea.CancellationToken.IsCancellationRequested)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await RejectUnprocessableAsync(ea, prepared.DiagnosticTypeName, exception).ConfigureAwait(false);
            return;
        }

        RecordOutcome(activity, outcome);
        switch (outcome.Settlement)
        {
            case DeliverySettlement.Acknowledge:
                await AckAsync(ea).ConfigureAwait(false);
                break;
            case DeliverySettlement.DeadLetter:
                await NackAsync(ea).ConfigureAwait(false);
                break;
            case DeliverySettlement.RedeliverLater:
                await RepublishForRetryAsync(outcome, ea).ConfigureAwait(false);
                await AckAsync(ea).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Rejects a delivery whose processing failed outside its consumers, for example on a header or an envelope field
    /// the worker cannot parse. Like a body the worker cannot deserialize, the delivery is logged and nacked without
    /// requeue: left unsettled, it would hold its prefetch slot and fail the same way after every restart. The callers
    /// let a failure during shutdown propagate instead, so the broker delivers that delivery again.
    /// </summary>
    private async Task RejectUnprocessableAsync(BasicDeliverEventArgs ea, string messageTypeName, Exception exception)
    {
        MessagingLog.UnprocessableDelivery(_logger, exception, _queueDefinition.Name, messageTypeName, ea.RoutingKey);
        await NackAsync(ea).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the handlers this delivery dispatches. A first delivery (no retry-handlers header) runs the
    /// full plan; a delayed-retry re-delivery carries the identities of the handlers that failed and runs only
    /// those. Identities that no longer match a registered handler (the consumer was renamed or removed since
    /// the re-publish) are logged and skipped; when none remain the caller acks the delivery without dispatch.
    /// </summary>
    private List<DeliveryHandler> ResolvePendingHandlers(RabbitMqPlan plan, BasicDeliverEventArgs ea)
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
    /// Records every failed attempt of the delivery on its receive activity, and sets the activity's status: OK when
    /// the delivery is acknowledged, an error when it is dead-lettered or re-published for a retry.
    /// </summary>
    private static void RecordOutcome(Activity? activity, DeliveryOutcome outcome)
    {
        if (activity is null)
        {
            return;
        }

        foreach (var failure in outcome.Failures)
        {
            activity.AddException(failure);
        }

        switch (outcome.Settlement)
        {
            case DeliverySettlement.Acknowledge:
                activity.SetStatus(ActivityStatusCode.Ok);
                break;
            case DeliverySettlement.DeadLetter:
            case DeliverySettlement.RedeliverLater:
                activity.SetStatus(ActivityStatusCode.Error, outcome.Failures[^1].Message);
                break;
        }
    }

    /// <summary>
    /// Re-publishes the delivery to the queue's retry queue for delayed re-delivery (see
    /// <see cref="RabbitMqOutgoingMessages.Retry"/>); the caller acks the original delivery afterwards.
    /// </summary>
    private Task RepublishForRetryAsync(DeliveryOutcome outcome, BasicDeliverEventArgs ea)
        => PublishThroughGateAsync(RabbitMqOutgoingMessages.Retry(
            ea, _queueDefinition.Name, outcome.RedeliveryRetryCount, outcome.RedeliveryHandlerIdentities, outcome.RedeliveryDelay));

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
    /// Publishes the fault of a consumer that failed for good (see
    /// <see cref="RabbitMqOutgoingMessages.Fault{TMessage}"/>), so a subscriber can deserialize it as
    /// <see cref="Fault{TMessage}"/> of the faulted message type. Publishing is best-effort: a failure to publish the
    /// fault is logged and never disrupts settling the original delivery.
    /// </summary>
    private async Task PublishFaultAsync<TMessage>(Fault<TMessage> fault, PreparedDelivery prepared, BasicDeliverEventArgs ea)
        where TMessage : notnull
    {
        var faultExchangeName = _messageConfigurationProvider.FaultExchangeName;
        try
        {
            var faultMessage = RabbitMqOutgoingMessages.Fault(fault, prepared.Envelope?.Message, ea, faultExchangeName, prepared.DiagnosticTypeName, _jsonOptions);
            await PublishThroughGateAsync(faultMessage).ConfigureAwait(false);
        }
        catch (Exception faultEx)
        {
            var (exchange, routingKey) = RabbitMqOutgoingMessages.ResolveFaultRoute(
                fault.OriginalContext.FaultAddress, faultExchangeName, prepared.DiagnosticTypeName);
            MessagingLog.FaultPublishFailed(_logger, faultEx, exchange, routingKey);
        }
    }

    /// <summary>
    /// Publishes a request consumer's reply (see <see cref="RabbitMqOutgoingMessages.Reply"/>). A request without a
    /// <c>ReplyTo</c> gets no reply.
    /// </summary>
    private Task SendReplyAsync(MessageEnvelope reply, BasicDeliverEventArgs ea)
        => RabbitMqOutgoingMessages.Reply(reply, ea, _jsonOptions) is { } replyMessage
            ? PublishThroughGateAsync(replyMessage)
            : Task.CompletedTask;

    /// <summary>
    /// Returns a copy of <paramref name="ea"/> whose <c>x-retry-count</c> header is set to
    /// <paramref name="retryCount"/>, so a consumer reading <see cref="IMessageContext.RetryCount"/> sees the
    /// current in-memory attempt. The delivery's AMQP properties are read-only on the receive side, hence the copy;
    /// <paramref name="ea"/> itself is left unchanged.
    /// </summary>
    internal static BasicDeliverEventArgs WithRetryCount(BasicDeliverEventArgs ea, int retryCount)
    {
        var headers = RabbitMqOutgoingMessages.CopyHeaders(ea);
        headers[RabbitMqConstants.RetryCountHeader] = retryCount;
        var properties = new BasicProperties(ea.BasicProperties) { Headers = headers };
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
            ? _plans.GetPlanByUrn(envelope.MessageType)
            : _plans.GetPlan(bareTypeName);

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
                await Task.WhenAll(pending).WaitAsync(DrainTimeout, _timeProvider).ConfigureAwait(false);
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

    /// <summary>
    /// The worker's <see cref="IDeliveryPort"/> for one delivery. The round comes from the delivery's
    /// <c>x-retry-count</c> header, and shutdown ends the delivery through the consumer's cancellation token. A failed
    /// handler goes back through the retry queue, unless the queue is partitioned: a partitioned queue keeps its order
    /// by retrying in-process.
    /// </summary>
    private sealed class DeliveryPort : IDeliveryPort
    {
        private readonly RabbitMqConsumerWorker _worker;
        private readonly PreparedDelivery _prepared;
        private readonly BasicDeliverEventArgs _delivery;

        public DeliveryPort(RabbitMqConsumerWorker worker, PreparedDelivery prepared, BasicDeliverEventArgs delivery)
        {
            _worker = worker;
            _prepared = prepared;
            _delivery = delivery;
            RetryCount = RabbitMqConstants.GetRetryCount(delivery.BasicProperties.Headers);
        }

        public int RetryCount { get; }

        public CancellationToken CancellationToken => _delivery.CancellationToken;

        public bool CanRedeliverLater => !_worker._partitioned;

        public MessageContext<TMessage> CreateContext<TMessage>(TMessage message, IServiceProvider services, int retryCount)
            where TMessage : notnull
        {
            var attempt = retryCount == RetryCount ? _delivery : WithRetryCount(_delivery, retryCount);
            var publisher = services.GetRequiredService<IPublisher>();
            var sendEndpointProvider = services.GetRequiredService<ISendEndpointProvider>();
            return _prepared.Envelope is null
                ? MessageContextFactory.CreateContext(message, attempt, publisher, sendEndpointProvider, _delivery.CancellationToken)
                : MessageContextFactory.CreateContext(message, attempt, _prepared.Envelope, publisher, sendEndpointProvider, _delivery.CancellationToken);
        }

        public Task WaitBeforeRetryAsync(TimeSpan delay)
            => delay <= TimeSpan.Zero
                ? Task.CompletedTask
                : Task.Delay(delay, _worker._timeProvider, _delivery.CancellationToken);

        public Task PublishFaultAsync<TMessage>(Fault<TMessage> fault)
            where TMessage : notnull
            => _worker.PublishFaultAsync(fault, _prepared, _delivery);

        public Task SendReplyAsync(MessageEnvelope reply) => _worker.SendReplyAsync(reply, _delivery);
    }
}
