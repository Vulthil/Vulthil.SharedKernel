using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Commands;

public sealed partial class WeatherAuditConsumer(
    ILogger<WeatherAuditConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<WeatherAuditEntry>
{
    public Task ConsumeAsync(IMessageContext<WeatherAuditEntry> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.SourceId, messageContext.Message.Location);
        tracker.Record("audit", messageContext.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Received WeatherAuditEntry for source {SourceId} at {Location}")]
    private static partial void LogReceived(ILogger logger, Guid sourceId, string location);
}
