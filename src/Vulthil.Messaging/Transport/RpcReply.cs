using System.Text.Json;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// The request/reply wire contract every transport shares. A reply is a <see cref="MessageEnvelope"/> that echoes the
/// request id the requester correlates on and the request's business correlation id. On success it carries the
/// response payload at the response type's URN; on failure it carries an <see cref="RpcFault"/> at
/// <see cref="RpcFault.UrnUri"/>. Transports build replies with <see cref="Success"/>, <see cref="Fault"/> and
/// <see cref="ShortCircuited"/>, and read them with <see cref="ToResult"/>, so a reply means the same on every transport.
/// </summary>
public static class RpcReply
{
    private const string ShortCircuitedMessage = "Consume pipeline did not produce a response (a filter likely short-circuited the chain).";

    /// <summary>
    /// Builds the reply that carries <paramref name="response"/> at its message type's URN.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="response">The response the request consumer produced.</param>
    /// <param name="provider">The provider that supplies the response URN and the JSON options.</param>
    /// <param name="requestId">The request id to echo, which the requester correlates the reply on.</param>
    /// <param name="correlationId">The request's business correlation id to echo.</param>
    /// <returns>The reply envelope.</returns>
    public static MessageEnvelope Success<TResponse>(TResponse response, IMessageConfigurationProvider provider, string? requestId, string? correlationId)
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(provider);

        return Create(
            provider.GetUrn(typeof(TResponse)),
            JsonSerializer.SerializeToElement(response, provider.JsonSerializerOptions),
            requestId,
            correlationId);
    }

    /// <summary>
    /// Builds the fault reply for <paramref name="exception"/>, which the request consumer threw. The fault carries
    /// the exception's message, type name and stack trace.
    /// </summary>
    /// <param name="exception">The exception the request consumer threw.</param>
    /// <param name="provider">The provider that supplies the JSON options.</param>
    /// <param name="requestId">The request id to echo, which the requester correlates the reply on.</param>
    /// <param name="correlationId">The request's business correlation id to echo.</param>
    /// <returns>The fault reply envelope.</returns>
    public static MessageEnvelope Fault(Exception exception, IMessageConfigurationProvider provider, string? requestId, string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return CreateFault(exception.Message, exception.GetType().FullName ?? "Unknown", exception.StackTrace, provider, requestId, correlationId);
    }

    /// <summary>
    /// Builds the fault reply for a consume pipeline that completed without producing a response, because a filter
    /// did not call the rest of the chain.
    /// </summary>
    /// <param name="provider">The provider that supplies the JSON options.</param>
    /// <param name="requestId">The request id to echo, which the requester correlates the reply on.</param>
    /// <param name="correlationId">The request's business correlation id to echo.</param>
    /// <returns>The fault reply envelope.</returns>
    public static MessageEnvelope ShortCircuited(IMessageConfigurationProvider provider, string? requestId, string? correlationId)
        => CreateFault(ShortCircuitedMessage, typeof(InvalidOperationException).FullName!, stackTrace: null, provider, requestId, correlationId);

    /// <summary>
    /// Reads <paramref name="reply"/> as the outcome of a request for <typeparamref name="TResponse"/>. The response
    /// payload becomes a success. An <see cref="RpcFault"/> becomes a <see cref="RequestErrorCodes.Failure"/> failure
    /// that carries the remote message. A reply of any other type, a null payload, or a payload that cannot be read
    /// becomes a <see cref="RequestErrorCodes.Deserialize"/> failure; a malformed reply never throws.
    /// </summary>
    /// <typeparam name="TResponse">The response type the request expects.</typeparam>
    /// <param name="reply">The reply envelope the transport received.</param>
    /// <param name="provider">The provider that supplies the response URN and the JSON options.</param>
    /// <returns>The outcome of the request.</returns>
    public static Result<TResponse> ToResult<TResponse>(MessageEnvelope reply, IMessageConfigurationProvider provider)
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(reply);
        ArgumentNullException.ThrowIfNull(provider);

        var options = provider.JsonSerializerOptions;
        try
        {
            if (reply.MessageType == provider.GetUrn(typeof(TResponse)))
            {
                var value = reply.Message.Deserialize<TResponse>(options);
                return value is not null
                    ? Result.Success(value)
                    : Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Deserialize, "Inner message deserialization failed."));
            }

            if (reply.MessageType == RpcFault.UrnUri)
            {
                var fault = reply.Message.Deserialize<RpcFault>(options);
                return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Failure, fault?.Message ?? "Unknown remote error"));
            }
        }
        catch (Exception exception)
        {
            return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Deserialize, $"Deserialization error: {exception.Message}"));
        }

        return Result.Failure<TResponse>(Error.Failure(RequestErrorCodes.Deserialize, $"Unexpected reply message type '{reply.MessageType}'."));
    }

    private static MessageEnvelope CreateFault(string message, string exceptionType, string? stackTrace, IMessageConfigurationProvider provider, string? requestId, string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var fault = new RpcFault
        {
            Message = message,
            ExceptionType = exceptionType,
            StackTrace = stackTrace,
            FaultedAt = DateTimeOffset.UtcNow,
        };

        return Create(RpcFault.UrnUri, JsonSerializer.SerializeToElement(fault, provider.JsonSerializerOptions), requestId, correlationId);
    }

    private static MessageEnvelope Create(Uri messageType, JsonElement message, string? requestId, string? correlationId)
        => new()
        {
            MessageId = Guid.CreateVersion7().ToString(),
            RequestId = requestId,
            CorrelationId = correlationId,
            MessageType = messageType,
            Message = message,
            SentTime = DateTimeOffset.UtcNow,
        };
}
