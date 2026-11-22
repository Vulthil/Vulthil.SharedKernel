using Microsoft.Extensions.Logging;
using Orders.Application.Orders.Place;
using Orders.Domain.Fulfillments;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Orders.Application.Fulfillments.Start;

/// <summary>
/// Starts the fulfillment of a placed order. The inbox deduplicates the event on its order id, so a redelivery never
/// starts a second fulfillment.
/// </summary>
public sealed class StartFulfillmentConsumer(ILogger<StartFulfillmentConsumer> logger, IOrdersDbContext dbContext, TimeProvider timeProvider) : IConsumer<OrderPlacedIntegrationEvent>
{
    public async Task ConsumeAsync(IMessageContext<OrderPlacedIntegrationEvent> messageContext, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting the fulfillment of order {OrderId} (routing key {RoutingKey})", messageContext.Message.OrderId, messageContext.RoutingKey);

        dbContext.Fulfillments.Add(Fulfillment.Start(messageContext.Message.OrderId, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
