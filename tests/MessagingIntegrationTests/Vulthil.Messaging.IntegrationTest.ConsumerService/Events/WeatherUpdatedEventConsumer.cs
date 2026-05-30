using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Events;

public sealed partial class WeatherUpdatedEventConsumer(
    ILogger<WeatherUpdatedEventConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<WeatherUpdatedEvent>
{
    public Task ConsumeAsync(IMessageContext<WeatherUpdatedEvent> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.Id, messageContext.Message.Location);
        tracker.Record("events", messageContext.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Received WeatherUpdatedEvent {Id} for {Location}")]
    private static partial void LogReceived(ILogger logger, Guid id, string location);
}
