using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Domain.Fulfillments;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Results;

namespace Orders.Application.Shipping;

/// <summary>
/// Completes the fulfillment of the delivered order. A delivery can arrive before the fulfillment exists (it is
/// started by a consumer on another queue), so a missing fulfillment throws and the delivery is retried.
/// </summary>
public sealed class ShipmentDeliveredConsumer(IOrdersDbContext dbContext, ILogger<ShipmentDeliveredConsumer> logger) : IConsumer<ShipmentDelivered>
{
    public async Task ConsumeAsync(IMessageContext<ShipmentDelivered> messageContext, CancellationToken cancellationToken = default)
    {
        var delivered = messageContext.Message;

        var fulfillments = await dbContext.Fulfillments
            .Where(fulfillment => fulfillment.OrderId == delivered.OrderReference)
            .ToListAsync(cancellationToken);
        var fulfillment = fulfillments.FirstOrDefault(f => f.Status is FulfillmentStatus.InProgressStatus)
            ?? fulfillments.FirstOrDefault()
            ?? throw new InvalidOperationException($"Order {delivered.OrderReference} has no fulfillment yet.");

        await fulfillment.Complete(delivered.DeliveredAtUtc, delivered.TrackingEventCount)
            .TapError(error => logger.LogInformation("Shipment {ShipmentId} changed nothing: {Error}", delivered.ShipmentId, error.Description))
            .TapAsync(() => dbContext.SaveChangesAsync(cancellationToken));
    }
}
