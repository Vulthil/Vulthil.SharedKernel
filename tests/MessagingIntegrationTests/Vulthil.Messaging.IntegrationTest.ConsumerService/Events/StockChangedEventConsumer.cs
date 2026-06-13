using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Events;

public sealed partial class StockChangedEventConsumer(
    ILogger<StockChangedEventConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<StockChangedEvent>
{
    public Task ConsumeAsync(IMessageContext<StockChangedEvent> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.Sku, messageContext.Message.Delta);
        tracker.Record("inventory.stock-changed", messageContext.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "StockChangedEventConsumer received {Sku} delta {Delta}")]
    private static partial void LogReceived(ILogger logger, string sku, int delta);
}
