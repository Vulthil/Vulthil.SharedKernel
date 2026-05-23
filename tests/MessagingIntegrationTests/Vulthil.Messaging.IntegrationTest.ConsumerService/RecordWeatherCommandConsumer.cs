using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService;

public sealed partial class RecordWeatherCommandConsumer(
    ILogger<RecordWeatherCommandConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<RecordWeatherCommand>
{
    public Task ConsumeAsync(IMessageContext<RecordWeatherCommand> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.Id, messageContext.Message.Location);
        tracker.RecordCommand(messageContext.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Received RecordWeatherCommand {Id} for {Location}")]
    private static partial void LogReceived(ILogger logger, Guid id, string location);
}
