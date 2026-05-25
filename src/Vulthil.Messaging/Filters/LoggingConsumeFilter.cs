using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Filters;

/// <summary>
/// Default open-generic consume filter that emits structured Debug logs on consume entry/exit
/// and a Warning log on uncaught exceptions, with timing information. Registered as the
/// outermost filter by <c>AddMessaging</c> when <see cref="ConsumeFilterOptions.EnableLogging"/>
/// is <see langword="true"/> (the default); otherwise it is not registered at all.
/// </summary>
/// <typeparam name="TMessage">The consumed message type.</typeparam>
internal sealed class LoggingConsumeFilter<TMessage>(
    ILogger<LoggingConsumeFilter<TMessage>> logger) : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    private readonly ILogger<LoggingConsumeFilter<TMessage>> _logger = logger;

    public async Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
    {
        var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;
        FilterLog.Consuming(_logger, messageType, context.MessageId, context.CorrelationId);

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
            var elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            FilterLog.Consumed(_logger, messageType, context.MessageId, elapsedMs);
        }
        catch (Exception ex)
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            FilterLog.ConsumeFailed(_logger, ex, messageType, context.MessageId, elapsedMs);
            throw;
        }
    }
}

internal static partial class FilterLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug,
        Message = "Consuming {MessageType} (messageId={MessageId}, correlationId={CorrelationId})")]
    public static partial void Consuming(ILogger logger, string messageType, string? messageId, string? correlationId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
        Message = "Consumed {MessageType} (messageId={MessageId}) in {ElapsedMs}ms")]
    public static partial void Consumed(ILogger logger, string messageType, string? messageId, long elapsedMs);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Consume of {MessageType} (messageId={MessageId}) failed after {ElapsedMs}ms")]
    public static partial void ConsumeFailed(ILogger logger, Exception exception, string messageType, string? messageId, long elapsedMs);
}
