using System.Text.Json;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class ResponseWaiter<T>(
    TaskCompletionSource<Result<T>> tcs,
    IMessageConfigurationProvider provider) : IResponseWaiter where T : notnull
{
    /// <summary>
    /// Completes the pending request by deserializing the reply <see cref="MessageEnvelope"/> and resolving the
    /// awaiting task with what <see cref="RpcReply.ToResult{TResponse}"/> reads from it. Bytes that are not an
    /// envelope yield a <see cref="RequestErrorCodes.Deserialize"/> failure.
    /// </summary>
    /// <param name="body">The raw reply payload received on the reply queue.</param>
    public void Complete(ReadOnlySpan<byte> body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(body, provider.JsonSerializerOptions);
            tcs.TrySetResult(envelope is null
                ? Result.Failure<T>(Error.Failure(RequestErrorCodes.Deserialize, "Reply envelope was null."))
                : RpcReply.ToResult<T>(envelope, provider));
        }
        catch (Exception ex)
        {
            tcs.TrySetResult(Result.Failure<T>(Error.Failure(RequestErrorCodes.Deserialize, $"Deserialization error: {ex.Message}")));
        }
    }
}
