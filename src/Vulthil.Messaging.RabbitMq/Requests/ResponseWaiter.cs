using System.Text.Json;
using Vulthil.Messaging.RabbitMq.Envelope;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class ResponseWaiter<T>(
    TaskCompletionSource<Result<T>> tcs,
    JsonSerializerOptions options,
    Uri responseUrn) : IResponseWaiter where T : notnull
{
    /// <summary>
    /// Completes the pending request by deserializing the reply <see cref="MessageEnvelope"/> and resolving
    /// the awaiting task. A reply whose message type is the response URN yields a success; the RPC fault URN
    /// (<see cref="RpcFault.Urn"/>) yields a failure carrying the remote error; anything else is a protocol error.
    /// </summary>
    /// <param name="body">The raw reply payload received on the reply queue.</param>
    public void Complete(ReadOnlySpan<byte> body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(body, options);
            if (envelope is null)
            {
                tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Deserialize", "Reply envelope was null.")));
                return;
            }

            if (envelope.MessageType == responseUrn)
            {
                var value = envelope.Message.Deserialize<T>(options);
                tcs.TrySetResult(value is not null
                    ? Result.Success(value)
                    : Result.Failure<T>(Error.Failure("Messaging.Request.Deserialize", "Inner message deserialization failed.")));
                return;
            }

            if (envelope.MessageType == RpcFault.UrnUri)
            {
                var fault = envelope.Message.Deserialize<RpcFault>(options);
                tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Failure", fault?.Message ?? "Unknown remote error")));
                return;
            }

            tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Deserialize", $"Unexpected reply message type '{envelope.MessageType}'.")));
        }
        catch (Exception ex)
        {
            tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Deserialize", $"Deserialization error: {ex.Message}")));
        }
    }
}
