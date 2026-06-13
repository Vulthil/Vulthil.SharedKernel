using System.Text.Json;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;
using Vulthil.Results;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory <see cref="IRequester"/>: captures the request, dispatches it to a registered responder or request
/// consumer, and maps the reply envelope to a <see cref="Result{TResponse}"/> the same way a broker transport
/// does — the response payload on success, an <see cref="RpcFault"/> on failure.
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
            await configureContext(context);
        }

        var requestId = Guid.CreateVersion7().ToString();
        var envelope = OutgoingEnvelope.Build(_provider, message, context, requestId);
        _harness.RecordRequested(message, envelope);

        var reply = await _transport.DeliverRequestAsync(envelope, cancellationToken);
        return MapReply<TResponse>(reply);
    }

    private Result<TResponse> MapReply<TResponse>(MessageEnvelope? reply)
        where TResponse : notnull
    {
        if (reply is null)
        {
            return Result.Failure<TResponse>(Error.NotFound(
                "Messaging.Request.NoConsumer",
                "No request consumer or responder is registered for the request type."));
        }

        var options = _provider.JsonSerializerOptions;

        if (reply.MessageType == _provider.GetUrn(typeof(TResponse)))
        {
            var value = reply.Message.Deserialize<TResponse>(options);
            return value is not null
                ? Result.Success(value)
                : Result.Failure<TResponse>(Error.Failure("Messaging.Request.Deserialize", "Inner message deserialization failed."));
        }

        if (reply.MessageType == RpcFault.UrnUri)
        {
            var fault = reply.Message.Deserialize<RpcFault>(options);
            return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Failure", fault?.Message ?? "Unknown remote error"));
        }

        return Result.Failure<TResponse>(Error.Failure("Messaging.Request.Deserialize", $"Unexpected reply message type '{reply.MessageType}'."));
    }
}
