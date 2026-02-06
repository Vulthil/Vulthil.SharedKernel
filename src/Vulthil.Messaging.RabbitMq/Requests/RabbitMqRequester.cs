using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class RabbitMqRequester(
    IInternalPublisher publisher,
    ResponseListener listener,
    IOptions<MessagingOptions> messagingOptions) : IRequester
{
    private readonly IInternalPublisher _publisher = publisher;
    private readonly ResponseListener _listener = listener;
    private readonly MessagingOptions _messagingOptions = messagingOptions.Value;
    private JsonSerializerOptions _jsonOptions => _messagingOptions.JsonSerializerOptions;
    private TimeSpan _defaultTimeout => _messagingOptions.DefaultTimeout;

    public async Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
        TRequest message,
        string? routingKey = null,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull
    {
        var tcs = new TaskCompletionSource<Result<TResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeoutCts = new CancellationTokenSource(_defaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var type = message.GetType();

        var finalRoutingKey = routingKey
            ?? RabbitMqConstants.GetMetadata(type, message, _messagingOptions.ReadOnlyRoutingKeyFormatters)
            ?? string.Empty;

        var correlationId = RabbitMqConstants.GetMetadata(type, message, _messagingOptions.ReadOnlyCorrelationIdFormatters)
            ?? Guid.CreateVersion7().ToString();

        _listener.RegisterWaiter(correlationId, tcs);

        try
        {
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = _listener.ReplyToQueueName,
                ContentType = RabbitMqConstants.ContentType,
                Type = typeof(TRequest).FullName,
                Expiration = _defaultTimeout.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);

            await _publisher.InternalPublishAsync<TRequest>(body, props, finalRoutingKey, cancellationToken);

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
