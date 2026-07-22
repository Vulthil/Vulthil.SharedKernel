using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class RabbitMqRequester : IRequester
{
    private readonly IInternalPublisher _publisher;
    private readonly ResponseListener _listener;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;

    // Deliberately the bus-scoped internal status rather than the public ITransport.WaitUntilReadyAsync seam:
    // ITransport is registered to support being swapped out (ConsumerHostedService resolves the last-registered
    // one, and Vulthil.Messaging.TestHarness relies on that to replace it), so a generic ITransport injected here
    // could resolve to a different transport than the RabbitMqBus this requester actually publishes through.
    // RabbitMqBusStartupStatus is registered once per RabbitMQ transport and is unambiguous — RabbitMqBus's own
    // WaitUntilReadyAsync implementation wraps this exact same signal for external callers.
    private readonly RabbitMqBusStartupStatus _startupStatus;
    private readonly ILogger<RabbitMqRequester> _logger;

    public RabbitMqRequester(
        IInternalPublisher publisher,
        ResponseListener listener,
        IMessageConfigurationProvider messageConfigurationProvider,
        RabbitMqBusStartupStatus startupStatus,
        ILogger<RabbitMqRequester> logger)
    {
        _publisher = publisher;
        _listener = listener;
        _messageConfigurationProvider = messageConfigurationProvider;
        _startupStatus = startupStatus;
        _logger = logger;
    }

    private JsonSerializerOptions JsonOptions => _messageConfigurationProvider.JsonSerializerOptions;

    public Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
       TRequest message,
       CancellationToken cancellationToken)
       where TRequest : notnull
       where TResponse : notnull => RequestAsync<TRequest, TResponse>(message, null, cancellationToken);

    public async Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
        TRequest message,
        Func<IRequestContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(message);
        var requestContext = new RequestContext();
        configureContext ??= (_ => ValueTask.CompletedTask);
        await configureContext(requestContext).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<Result<TResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var timeout = requestContext.Timeout ?? _messageConfigurationProvider.DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var type = message.GetType();
        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var routingKey = requestContext.RoutingKey
            ?? messageConfiguration.RoutingKeyFormatter?.Invoke(message)
            ?? string.Empty;

        // A dedicated per-request id correlates the reply back to this call. It is carried in the AMQP
        // CorrelationId property (the RPC slot the reply echoes) and the envelope's RequestId, leaving the
        // business CorrelationId free — two requests sharing a business key cannot collide on the waiter.
        var ids = RabbitMqWireMessageBuilder.ResolveIds(message, requestContext, messageConfiguration);
        var requestId = Guid.CreateVersion7().ToString();
        var responseUrn = _messageConfigurationProvider.GetUrn(typeof(TResponse));
        var exchange = messageConfiguration.Exchange;

        // The bus starts in the background, so a request issued while the host is still warming up would be
        // published before the responder's queue and bindings exist and expire unanswered. Hold the request until
        // the bus has declared its topology and started its consumers (mirroring the outbox relay), bounded by the
        // request timeout.
        try
        {
            await _startupStatus.Ready.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.IsCancellationRequested)
            {
                MessagingLog.RequestTimedOut(_logger, ids.UrnString, ids.CorrelationId, timeout.TotalSeconds);
                return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Timeout", $"Request timed out after {timeout.TotalSeconds}s waiting for the transport to start"));
            }

            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Cancelled", "Request was cancelled by user."));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.TransportUnavailable", $"The transport failed to start: {ex.Message}"));
        }

        var replyQueue = await _listener.GetReplyToQueueNameAsync(cancellationToken).ConfigureAwait(false);
        var replyTo = RabbitMqAddress.ResolveRoutingKey(requestContext.ResponseAddress) ?? replyQueue;

        using var activity = RabbitMqWireMessageBuilder.StartProducerActivity(
            $"{exchange} request", "request", exchange, routingKey, ids.UrnString, ids.MessageId, ids.CorrelationId);

        _listener.RegisterWaiter(requestId, tcs, responseUrn);
        MessagingLog.RequestSending(_logger, ids.UrnString, ids.CorrelationId, timeout.TotalSeconds);

        try
        {
            var props = RabbitMqWireMessageBuilder.CreateBaseProperties(ids.UrnString, ids.MessageId, requestContext.Headers);
            props.CorrelationId = requestId;
            props.ReplyTo = replyTo;
            props.Expiration = timeout.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);

            var body = RabbitMqWireMessageBuilder.SerializeEnvelope(
                message, requestContext, ids.MessageId, ids.CorrelationId, ids.Urn, JsonOptions, requestId);

            await _publisher.InternalPublishAsync(body, props, routingKey, messageConfiguration, cancellationToken).ConfigureAwait(false);

            await using var ctRegistration = linkedCts.Token.Register(() =>
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    MessagingLog.RequestTimedOut(_logger, ids.UrnString, ids.CorrelationId, timeout.TotalSeconds);
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Timeout", $"Request timed out after {timeout.TotalSeconds}s")));
                }
                else
                {
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Cancelled", "Request was cancelled by user.")));
                }
            }).ConfigureAwait(false);

            var result = await tcs.Task.ConfigureAwait(false);
            activity?.SetStatus(result.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error, result.IsSuccess ? null : result.Error.Description);
            MessagingLog.RequestCompleted(_logger, ids.UrnString, ids.CorrelationId, result.IsSuccess);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Publish", $"Publishing error: {ex.Message}"));
        }
        finally
        {
            _listener.RemoveWaiter(requestId);
        }
    }
}
