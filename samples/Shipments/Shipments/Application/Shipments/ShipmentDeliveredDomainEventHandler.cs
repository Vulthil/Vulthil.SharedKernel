using Microsoft.EntityFrameworkCore;
using Shipments.Application.Notifications;
using Shipments.Domain.Events;
using Shipments.Infrastructure;
using Shipments.Messages;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.SharedKernel.Events;

namespace Shipments.Application.Shipments;

/// <summary>
/// Runs when the outbox relay dispatches <see cref="ShipmentDeliveredDomainEvent"/>. The relay dispatches inside its
/// own transaction, so the transactional outbox captures the <see cref="ShipmentDelivered"/> publish and relays it to
/// the broker once that transaction commits.
/// </summary>
public sealed class ShipmentDeliveredDomainEventHandler(
    ShipmentsDbContext dbContext,
    IPublisher publisher,
    ICustomerNotifier customerNotifier) : IDomainEventHandler<ShipmentDeliveredDomainEvent>
{
    public async Task HandleAsync(ShipmentDeliveredDomainEvent notification, CancellationToken cancellationToken = default)
    {
        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .Include(s => s.TrackingEvents)
            .SingleAsync(s => s.Id == notification.ShipmentId, cancellationToken);

        await publisher.PublishAsync(
            new ShipmentDelivered(
                shipment.Id.Value,
                shipment.OrderReference,
                shipment.Carrier,
                notification.DeliveredAtUtc,
                shipment.TrackingEvents.Count),
            cancellationToken);

        await customerNotifier.NotifyDeliveredAsync(
            new DeliveryNotification(shipment.Id.Value, shipment.Recipient, notification.DeliveredAtUtc),
            cancellationToken);
    }
}
