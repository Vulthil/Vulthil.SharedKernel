using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
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
    private TimeSpan DefaultTimeout => _messageConfigurationProvider.DefaultTimeout;

    public Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
       TRequest message,
       CancellationToken cancellationToken)
       where TRequest : notnull
       where TResponse : notnull => RequestAsync<TRequest, TResponse>(message, null, cancellationToken);

    public async Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
        TRequest message,
        Func<IPublishContext, Task>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(message);
        var publishContext = new PublishContext();
        configureContext ??= (_ => Task.CompletedTask);
        await configureContext(publishContext);

        var tcs = new TaskCompletionSource<Result<TResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var type = message.GetType();
        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var routingKey = publishContext.RoutingKey
            ?? messageConfiguration.RoutingKeyFormatter?.Invoke(message)
            ?? string.Empty;

        var correlationId = publishContext.CorrelationId
                ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
                ?? Guid.CreateVersion7().ToString();

        var messageId = publishContext.MessageId ?? Guid.CreateVersion7().ToString();
        var exchange = messageConfiguration.Exchange;

        var replyQueue = await _listener.GetReplyToQueueNameAsync(cancellationToken);
        var replyTo = PublishContext.ResolveRoutingKeyFromUri(publishContext.ResponseAddress) ?? replyQueue;

        using var activity = MessagingInstrumentation.ActivitySource.StartActivity(
            $"{exchange} request",
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, "request");
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, exchange);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, routingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, type.FullName);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, messageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, correlationId);
        }

        _listener.RegisterWaiter(correlationId, tcs);
        MessagingLog.RequestSending(_logger, type.FullName ?? type.Name, correlationId, DefaultTimeout.TotalSeconds);

        try
        {
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = replyTo,
                ContentType = RabbitMqConstants.ContentType,
                Type = type.FullName,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Expiration = DefaultTimeout.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                Headers = publishContext.Headers,
                MessageId = messageId,
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

            await _publisher.InternalPublishAsync<TRequest>(body, props, routingKey, messageConfiguration, cancellationToken);

            await using var ctRegistration = linkedCts.Token.Register(() =>
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    MessagingLog.RequestTimedOut(_logger, type.FullName ?? type.Name, correlationId, DefaultTimeout.TotalSeconds);
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Timeout", $"Request timed out after {DefaultTimeout.TotalSeconds}s")));
                }
                else
                {
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Cancelled", "Request was cancelled by user.")));
                }
            });

            var result = await tcs.Task;
            activity?.SetStatus(result.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error, result.IsSuccess ? null : result.Error.Description);
            MessagingLog.RequestCompleted(_logger, type.FullName ?? type.Name, correlationId, result.IsSuccess);
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
