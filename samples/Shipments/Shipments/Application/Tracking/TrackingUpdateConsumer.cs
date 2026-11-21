using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Shipments.Domain;
using Shipments.Infrastructure;
using Shipments.Messages;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Results;

namespace Shipments.Application.Tracking;

/// <summary>
/// One consumer for every <see cref="TrackingUpdate"/> variant: the queue subscribes to each concrete type, and the
/// delivery is dispatched to this consumer of the base type. The queue is partitioned by shipment, so the scans of
/// one shipment are applied one at a time and in the order the carrier published them.
/// </summary>
public sealed class TrackingUpdateConsumer(ShipmentsDbContext dbContext, ILogger<TrackingUpdateConsumer> logger) : IConsumer<TrackingUpdate>
{
    public async Task ConsumeAsync(IMessageContext<TrackingUpdate> messageContext, CancellationToken cancellationToken = default)
    {
        var update = messageContext.Message;
        var shipmentId = new ShipmentId(update.ShipmentId);

        var shipment = await dbContext.Shipments
            .Include(s => s.TrackingEvents)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken)
            ?? throw new ShipmentNotFoundException(update.ShipmentId);

        await shipment.RecordTrackingUpdate(StatusOf(update), update.Location, update.OccurredAtUtc)
            .TapError(error => logger.LogInformation(
                "Skipped {Update} for shipment {ShipmentId}: {Error}",
                update.GetType().Name,
                update.ShipmentId,
                error.Description))
            .TapAsync(() => dbContext.SaveChangesAsync(cancellationToken));
    }

    private static ShipmentStatus StatusOf(TrackingUpdate update) => update switch
    {
        ParcelPickedUp => ShipmentStatus.PickedUp,
        ParcelInTransit => ShipmentStatus.InTransit,
        ParcelDelivered => ShipmentStatus.Delivered,
        _ => throw new UnreachableException($"Unknown tracking update {update.GetType().Name}."),
    };
}
