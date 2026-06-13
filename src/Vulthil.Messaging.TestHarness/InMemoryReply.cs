using System.Text.Json;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// Builds reply <see cref="MessageEnvelope"/> instances for request/reply dispatch, mirroring the wire shape a
/// real transport produces: a success carries the response payload at the response URN, a failure carries an
/// <see cref="RpcFault"/> at <see cref="RpcFault.UrnUri"/>.
/// </summary>
internal static class InMemoryReply
{
    public static MessageEnvelope Build(Uri messageType, JsonElement message, MessageEnvelope requestEnvelope)
        => new()
        {
            MessageId = Guid.CreateVersion7().ToString(),
            RequestId = requestEnvelope.RequestId,
            CorrelationId = requestEnvelope.CorrelationId,
            MessageType = messageType,
            Message = message,
            SentTime = DateTimeOffset.UtcNow,
        };

    public static MessageEnvelope BuildFault(Exception exception, JsonSerializerOptions options, MessageEnvelope requestEnvelope)
        => BuildFault(exception.Message, exception.GetType().FullName ?? "Unknown", exception.StackTrace, options, requestEnvelope);

    public static MessageEnvelope BuildFault(string message, string exceptionType, string? stackTrace, JsonSerializerOptions options, MessageEnvelope requestEnvelope)
    {
        var fault = new RpcFault
        {
            Message = message,
            ExceptionType = exceptionType,
            StackTrace = stackTrace,
            FaultedAt = DateTimeOffset.UtcNow,
        };
        return Build(RpcFault.UrnUri, JsonSerializer.SerializeToElement(fault, options), requestEnvelope);
    }
}
