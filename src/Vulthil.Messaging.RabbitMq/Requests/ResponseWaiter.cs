using System.Text.Json;
using Vulthil.Results;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class ResponseWaiter<T>(
    TaskCompletionSource<Result<T>> tcs,
    JsonSerializerOptions options) : IResponseWaiter where T : notnull
{
    public void Complete(ReadOnlySpan<byte> body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageResult>(body, options);

            if (envelope is { IsSuccess: true })
            {
                var innerResult = JsonSerializer.Deserialize<T>(envelope.Value, options);
                if (innerResult is not null)
                {
                    tcs.TrySetResult(Result.Success(innerResult));
                }
                else
                {
                    tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Deserialize", "Inner message deserialization failed.")));
                }
            }
            else
            {
                tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Failure", envelope?.ErrorMessage ?? "Unknown remote error")));
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetResult(Result.Failure<T>(Error.Failure("Messaging.Request.Deserialize", $"Deserialization error: {ex.Message}")));
        }
    }
}
