using Microsoft.Extensions.Logging;

namespace Vulthil.Messaging.RabbitMq.Logging;

internal static partial class MessagingLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "RabbitMQ bus starting: declaring topology for {QueueCount} queue(s)")]
    public static partial void BusStarting(ILogger logger, int queueCount);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "RabbitMQ bus started")]
    public static partial void BusStarted(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug,
        Message = "Declared queue '{Queue}' with {RegistrationCount} consumer registration(s)")]
    public static partial void QueueDeclared(ILogger logger, string queue, int registrationCount);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Debug,
        Message = "Consumer worker started: queue='{Queue}', channel={ChannelIndex}, prefetch={Prefetch}, concurrency={Concurrency}")]
    public static partial void WorkerStarted(ILogger logger, string queue, int channelIndex, ushort prefetch, ushort concurrency);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning,
        Message = "Received message with no execution plan: queue='{Queue}', type='{MessageType}', routingKey='{RoutingKey}'. Acking and dropping.")]
    public static partial void NoExecutionPlan(ILogger logger, string queue, string messageType, string routingKey);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error,
        Message = "Poison message on queue '{Queue}' (type='{MessageType}', routingKey='{RoutingKey}'): payload could not be deserialized. Nacking without requeue.")]
    public static partial void PoisonMessage(ILogger logger, Exception exception, string queue, string messageType, string routingKey);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning,
        Message = "Consumer '{Consumer}' threw on queue '{Queue}' (routingKey='{RoutingKey}', retry={Retry}/{MaxRetry})")]
    public static partial void ConsumerThrew(ILogger logger, Exception exception, string queue, string consumer, string routingKey, int retry, int maxRetry);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Debug,
        Message = "Scheduling retry {Retry}/{MaxRetry} on queue '{Queue}' after delay {Delay}")]
    public static partial void SchedulingRetry(ILogger logger, string queue, int retry, int maxRetry, TimeSpan delay);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Error,
        Message = "Consumer '{Consumer}' permanently failed on queue '{Queue}' (type='{MessageType}', routingKey='{RoutingKey}'). Publishing fault.")]
    public static partial void ConsumerFailed(ILogger logger, Exception exception, string queue, string consumer, string messageType, string routingKey);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Error,
        Message = "Failed to publish fault to exchange '{FaultExchange}' (routingKey='{RoutingKey}'). Original exception preserved.")]
    public static partial void FaultPublishFailed(ILogger logger, Exception exception, string faultExchange, string routingKey);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Warning,
        Message = "Retry re-delivery on queue '{Queue}' lists handler identities that are no longer registered: {MissingHandlers}. They will not be re-dispatched.")]
    public static partial void RetryHandlersMissing(ILogger logger, string queue, string missingHandlers);

    [LoggerMessage(EventId = 1108, Level = LogLevel.Warning,
        Message = "Retry policy on queue '{Queue}' ignores exception type '{TypeName}', which cannot be resolved to a CLR type; matching exceptions WILL be retried. Use the assembly-qualified type name.")]
    public static partial void IgnoredExceptionUnresolvable(ILogger logger, string queue, string typeName);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Debug,
        Message = "Publishing {MessageType} to exchange '{Exchange}' (routingKey='{RoutingKey}', messageId='{MessageId}')")]
    public static partial void Publishing(ILogger logger, string messageType, string exchange, string routingKey, string messageId);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Debug,
        Message = "Declared exchange '{Exchange}' of type {ExchangeType}")]
    public static partial void ExchangeDeclared(ILogger logger, string exchange, MessagingExchangeType exchangeType);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Debug,
        Message = "Sending {MessageType} to queue '{Queue}' (messageId='{MessageId}', correlationId='{CorrelationId}')")]
    public static partial void Sending(ILogger logger, string messageType, string queue, string messageId, string correlationId);

    [LoggerMessage(EventId = 1300, Level = LogLevel.Debug,
        Message = "Sending request {RequestType} (correlationId='{CorrelationId}', timeout={TimeoutSeconds}s)")]
    public static partial void RequestSending(ILogger logger, string requestType, string correlationId, double timeoutSeconds);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Debug,
        Message = "Request {RequestType} completed (correlationId='{CorrelationId}', success={Success})")]
    public static partial void RequestCompleted(ILogger logger, string requestType, string correlationId, bool success);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Warning,
        Message = "Request {RequestType} timed out after {TimeoutSeconds}s (correlationId='{CorrelationId}')")]
    public static partial void RequestTimedOut(ILogger logger, string requestType, string correlationId, double timeoutSeconds);

    [LoggerMessage(EventId = 1400, Level = LogLevel.Debug,
        Message = "Response listener started: replyTo='{ReplyQueue}'")]
    public static partial void ResponseListenerStarted(ILogger logger, string replyQueue);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Warning,
        Message = "Received response with no matching waiter (correlationId='{CorrelationId}')")]
    public static partial void ResponseWaiterNotFound(ILogger logger, string correlationId);
}
