using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
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
        await configureContext(requestContext);

        var tcs = new TaskCompletionSource<Result<TResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var timeout = requestContext.Timeout ?? _messageConfigurationProvider.DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var type = message.GetType();
        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var routingKey = requestContext.RoutingKey
            ?? messageConfiguration.RoutingKeyFormatter?.Invoke(message)
            ?? string.Empty;

        var correlationId = requestContext.CorrelationId
                ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
                ?? Guid.CreateVersion7().ToString();

        // A dedicated per-request id correlates the reply back to this call. It is carried in the AMQP
        // CorrelationId property (the RPC slot the reply echoes) and the envelope's RequestId, leaving the
        // business CorrelationId free — two requests sharing a business key no longer collide on the waiter.
        var requestId = Guid.CreateVersion7().ToString();
        var responseUrn = _messageConfigurationProvider.GetUrn(typeof(TResponse));

        var messageId = requestContext.MessageId ?? Guid.CreateVersion7().ToString();
        var exchange = messageConfiguration.Exchange;
        var urn = messageConfiguration.Urn;
        var urnString = urn.AbsoluteUri;

        // The bus starts in the background, so a request issued while the host is still warming up would be
        // published before the responder's queue and bindings exist and expire unanswered. Hold the request until
        // the bus has declared its topology and started its consumers (mirroring the outbox relay), bounded by the
        // request timeout.
        try
        {
            await _startupStatus.Ready.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.IsCancellationRequested)
            {
                MessagingLog.RequestTimedOut(_logger, urnString, correlationId, timeout.TotalSeconds);
                return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Timeout", $"Request timed out after {timeout.TotalSeconds}s waiting for the transport to start"));
            }

            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Cancelled", "Request was cancelled by user."));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.TransportUnavailable", $"The transport failed to start: {ex.Message}"));
        }

        var replyQueue = await _listener.GetReplyToQueueNameAsync(cancellationToken);
        var replyTo = RabbitMqAddress.ResolveRoutingKey(requestContext.ResponseAddress) ?? replyQueue;

        using var activity = MessagingInstrumentation.ActivitySource.StartActivity(
            $"{exchange} request",
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, "request");
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, exchange);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, routingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, urnString);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, messageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, correlationId);
        }

        _listener.RegisterWaiter(requestId, tcs, responseUrn);
        MessagingLog.RequestSending(_logger, urnString, correlationId, timeout.TotalSeconds);

        try
        {
            var props = new BasicProperties
            {
                CorrelationId = requestId,
                ReplyTo = replyTo,
                ContentType = RabbitMqConstants.ContentType,
                Type = urnString,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Expiration = timeout.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                Headers = new Dictionary<string, object?>(requestContext.Headers),
                MessageId = messageId,
            };

            var envelope = MessageEnvelopeFactory.Create(message, requestContext, messageId, correlationId, urn, JsonOptions, requestId);
            var body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);

            await _publisher.InternalPublishAsync<TRequest>(body, props, routingKey, messageConfiguration, cancellationToken);

            await using var ctRegistration = linkedCts.Token.Register(() =>
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    MessagingLog.RequestTimedOut(_logger, urnString, correlationId, timeout.TotalSeconds);
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Timeout", $"Request timed out after {timeout.TotalSeconds}s")));
                }
                else
                {
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Cancelled", "Request was cancelled by user.")));
                }
            });

            var result = await tcs.Task;
            activity?.SetStatus(result.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error, result.IsSuccess ? null : result.Error.Description);
            MessagingLog.RequestCompleted(_logger, urnString, correlationId, result.IsSuccess);
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
