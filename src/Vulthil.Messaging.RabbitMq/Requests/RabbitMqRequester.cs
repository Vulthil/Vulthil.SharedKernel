using System.Diagnostics;
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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RabbitMqRequester> _logger;

    public RabbitMqRequester(
        IInternalPublisher publisher,
        ResponseListener listener,
        IMessageConfigurationProvider messageConfigurationProvider,
        RabbitMqBusStartupStatus startupStatus,
        TimeProvider timeProvider,
        ILogger<RabbitMqRequester> logger)
    {
        _publisher = publisher;
        _listener = listener;
        _messageConfigurationProvider = messageConfigurationProvider;
        _startupStatus = startupStatus;
        _timeProvider = timeProvider;
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
        using var timeoutCts = new CancellationTokenSource(timeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var type = message.GetType();
        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(type);

        var requestId = Guid.CreateVersion7().ToString();

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
                var unsentIds = RabbitMqOutgoingMessages.ResolveIds(message, requestContext, messageConfiguration);
                MessagingLog.RequestTimedOut(_logger, unsentIds.UrnString, unsentIds.CorrelationId, timeout.TotalSeconds);
                return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Timeout, $"Request timed out after {timeout.TotalSeconds}s waiting for the transport to start"));
            }

            return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Cancelled, "Request was cancelled by user."));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.TransportUnavailable, $"The transport failed to start: {ex.Message}"));
        }

        var replyQueue = await _listener.GetReplyToQueueNameAsync(cancellationToken).ConfigureAwait(false);

        RabbitMqProducedMessage request;
        try
        {
            request = RabbitMqOutgoingMessages.Request(message, requestContext, messageConfiguration, requestId, replyQueue, timeout, JsonOptions);
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Publish, $"Publishing error: {ex.Message}"));
        }

        var ids = request.Ids;
        using var activity = request.StartActivity();

        _listener.RegisterWaiter(requestId, tcs);
        MessagingLog.RequestSending(_logger, ids.UrnString, ids.CorrelationId, timeout.TotalSeconds);

        try
        {
            await _publisher.InternalPublishAsync(request.Message, messageConfiguration, cancellationToken).ConfigureAwait(false);

            await using var ctRegistration = linkedCts.Token.Register(() =>
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    MessagingLog.RequestTimedOut(_logger, ids.UrnString, ids.CorrelationId, timeout.TotalSeconds);
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Timeout, $"Request timed out after {timeout.TotalSeconds}s")));
                }
                else
                {
                    tcs.TrySetResult(Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Cancelled, "Request was cancelled by user.")));
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
            return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Publish, $"Publishing error: {ex.Message}"));
        }
        finally
        {
            _listener.RemoveWaiter(requestId);
        }
    }
}
