using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Requests;

public sealed partial class GetWeatherRequestConsumer(
    ILogger<GetWeatherRequestConsumer> logger,
    ReceivedMessageTracker tracker,
    TimeProvider timeProvider) : IRequestConsumer<GetWeatherRequest, GetWeatherResponse>
{
    public Task<GetWeatherResponse> ConsumeAsync(IMessageContext<GetWeatherRequest> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.Location);
        tracker.Record("requests", messageContext.Message);

        var response = new GetWeatherResponse(
            Location: messageContext.Message.Location,
            TemperatureC: 21,
            RecordedAt: timeProvider.GetUtcNow());

        return Task.FromResult(response);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Received GetWeatherRequest for {Location}")]
    private static partial void LogReceived(ILogger logger, string location);
}
