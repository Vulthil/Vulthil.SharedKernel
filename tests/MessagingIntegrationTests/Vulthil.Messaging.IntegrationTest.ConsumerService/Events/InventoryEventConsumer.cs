using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Events;

public sealed partial class InventoryEventConsumer(
    ILogger<InventoryEventConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<IInventoryEvent>
{
    public Task ConsumeAsync(IMessageContext<IInventoryEvent> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.GetType().Name, messageContext.Message.Sku);
        tracker.Record("inventory.any", messageContext.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "InventoryEventConsumer received {EventType} for {Sku}")]
    private static partial void LogReceived(ILogger logger, string eventType, string sku);
}
