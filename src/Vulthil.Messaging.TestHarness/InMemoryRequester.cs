using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;
using Vulthil.Results;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory <see cref="IRequester"/>: captures the request, dispatches it to a registered responder or request
/// consumer, and reads the reply envelope into a <see cref="Result{TResponse}"/> with <see cref="RpcReply"/>, exactly
/// as a broker transport does — the response payload on success, an <see cref="RpcFault"/> on failure, and a
/// <see cref="RequestErrorCodes.Cancelled"/> failure when the caller's token ends the request first.
/// </summary>
internal sealed class InMemoryRequester : IRequester
{
    private readonly IMessageConfigurationProvider _provider;
    private readonly InMemoryTransport _transport;
    private readonly TestHarness _harness;

    public InMemoryRequester(IMessageConfigurationProvider provider, InMemoryTransport transport, TestHarness harness)
    {
        _provider = provider;
        _transport = transport;
        _harness = harness;
    }

    public Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken)
        where TRequest : notnull
        where TResponse : notnull
        => RequestAsync<TRequest, TResponse>(message, null, cancellationToken);

    public async Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(
        TRequest message,
        Func<IRequestContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new RequestContext();
        if (configureContext is not null)
        {
            await configureContext(context).ConfigureAwait(false);
        }

        var requestId = Guid.CreateVersion7().ToString();
        var envelope = OutgoingEnvelope.Build(_provider, message, context, requestId);
        _harness.RecordRequested(message, envelope);

        MessageEnvelope? reply;
        try
        {
            reply = await _transport.DeliverRequestAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Cancelled, "Request was cancelled by user."));
        }

        return MapReply<TResponse>(reply);
    }

    private Result<TResponse> MapReply<TResponse>(MessageEnvelope? reply)
        where TResponse : notnull
        => reply is null
            ? Result.Failure<TResponse>(Error.Failure(
                RequestErrorCodes.Timeout,
                "Request timed out — no consumer or responder is registered for the request type."))
            : RpcReply.ToResult<TResponse>(reply, _provider);
}
