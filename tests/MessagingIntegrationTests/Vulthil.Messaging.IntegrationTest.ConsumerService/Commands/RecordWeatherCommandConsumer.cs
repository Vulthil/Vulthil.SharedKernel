using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Commands;

public sealed partial class RecordWeatherCommandConsumer(
    ILogger<RecordWeatherCommandConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<RecordWeatherCommand>
{
    private static readonly Uri AuditQueue = new("queue:weather-audit");

    public async Task ConsumeAsync(IMessageContext<RecordWeatherCommand> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.Id, messageContext.Message.Location);
        tracker.Record("commands", messageContext.Message);

        await messageContext.SendAsync(
            AuditQueue,
            new WeatherAuditEntry(messageContext.Message.Id, messageContext.Message.Location));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Received RecordWeatherCommand {Id} for {Location}")]
    private static partial void LogReceived(ILogger logger, Guid id, string location);
}
