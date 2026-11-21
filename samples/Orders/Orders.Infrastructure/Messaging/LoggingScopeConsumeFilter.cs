using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;

namespace WebApi.Infrastructure.Messaging;

/// <summary>
/// An open-generic consume filter: one registration wraps the consumers of every message type. Every log entry a
/// consumer writes carries the message id, the correlation id and the sending service.
/// </summary>
public sealed class LoggingScopeConsumeFilter<TMessage>(ILogger<LoggingScopeConsumeFilter<TMessage>> logger) : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    public async Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = context.MessageId,
            ["CorrelationId"] = context.CorrelationId,
            ["SourceService"] = context.Headers.GetValueOrDefault(SourceServiceHeaderPublishFilter.HeaderName),
        });

        await next(context);
    }
}
