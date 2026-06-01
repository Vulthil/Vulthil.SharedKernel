using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Envelope;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class RabbitMqRequester : IRequester
{
    private readonly IInternalPublisher _publisher;
    private readonly ResponseListener _listener;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqRequester> _logger;

    public RabbitMqRequester(
        IInternalPublisher publisher,
        ResponseListener listener,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqRequester> logger)
    {
        _publisher = publisher;
        _listener = listener;
        _messageConfigurationProvider = messageConfigurationProvider;
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

        var messageId = requestContext.MessageId ?? Guid.CreateVersion7().ToString();
        var exchange = messageConfiguration.Exchange;
        var urn = messageConfiguration.Urn;
        var urnString = urn.AbsoluteUri;

        var replyQueue = await _listener.GetReplyToQueueNameAsync(cancellationToken);
        var replyTo = PublishContext.ResolveRoutingKeyFromUri(requestContext.ResponseAddress) ?? replyQueue;

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

        _listener.RegisterWaiter(correlationId, tcs);
        MessagingLog.RequestSending(_logger, urnString, correlationId, timeout.TotalSeconds);

        try
        {
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = replyTo,
                ContentType = RabbitMqConstants.ContentType,
                Type = urnString,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Expiration = timeout.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                Headers = requestContext.Headers,
                MessageId = messageId,
            };

            var envelope = MessageEnvelopeFactory.Create(message, requestContext, messageId, correlationId, urn, JsonOptions);
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
            _listener.RemoveWaiter(correlationId);
        }
    }
}
