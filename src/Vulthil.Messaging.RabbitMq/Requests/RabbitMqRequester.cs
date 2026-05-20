using System.Globalization;
using System.Text.Json;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class RabbitMqRequester(
    IInternalPublisher publisher,
    ResponseListener listener,
    IMessageConfigurationProvider messageConfigurationProvider) : IRequester
{
    private readonly IInternalPublisher _publisher = publisher;
    private readonly ResponseListener _listener = listener;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider = messageConfigurationProvider;
    private JsonSerializerOptions _jsonOptions => _messageConfigurationProvider.JsonSerializerOptions;
    private TimeSpan _defaultTimeout => _messageConfigurationProvider.DefaultTimeout;

    /// <summary>
    /// Executes this member.
    /// </summary>
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

        using var timeoutCts = new CancellationTokenSource(_defaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var type = message.GetType();

        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var routingKey = publishContext.RoutingKey
            ?? messageConfiguration.RoutingKeyFormatter?.Invoke(message)
            ?? string.Empty;

        var correlationId = publishContext.CorrelationId
                ?? messageConfiguration.CorrelationIdFormatter?.Invoke(message)
                ?? Guid.CreateVersion7().ToString();

        _listener.RegisterWaiter(correlationId, tcs);

        try
        {
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = PublishContext.ResolveRoutingKeyFromUri(publishContext.ResponseAddress) ?? _listener.ReplyToQueueName,
                ContentType = RabbitMqConstants.ContentType,
                Type = typeof(TRequest).FullName,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Expiration = _defaultTimeout.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                Headers = publishContext.Headers,
                MessageId = publishContext.MessageId ?? Guid.CreateVersion7().ToString(),
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);

            await _publisher.InternalPublishAsync<TRequest>(body, props, routingKey, messageConfiguration, cancellationToken);

            using var ctRegistration = linkedCts.Token.Register(() =>
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Timeout", $"Request timed out after {_defaultTimeout.TotalSeconds}s")));
                }
                else
                {
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure("Messaging.Request.Cancelled", "Request was cancelled by user.")));
                }
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Publish", $"Publishing error: {ex.Message}"));
        }
        finally
        {
            _listener.RemoveWaiter(correlationId);
        }
    }
}
