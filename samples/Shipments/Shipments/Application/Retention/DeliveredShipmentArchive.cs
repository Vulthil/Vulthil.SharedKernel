using Microsoft.EntityFrameworkCore;
using Shipments.Domain;
using Shipments.Infrastructure;

namespace Shipments.Application.Retention;

/// <summary>
/// The store the delivered-shipment retention sweep prunes. The sweep calls <see cref="DeleteDeliveredAsync"/> until a
/// call deletes fewer rows than a full batch.
/// </summary>
public sealed class DeliveredShipmentArchive(ShipmentsDbContext dbContext)
{
    public async Task<int> DeleteDeliveredAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
    {
        var expired = await dbContext.Shipments
            .Where(shipment => shipment.Status == ShipmentStatus.Delivered && shipment.DeliveredAtUtc < olderThanUtc)
            .OrderBy(shipment => shipment.DeliveredAtUtc)
            .Select(shipment => shipment.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        return await dbContext.Shipments
            .Where(shipment => expired.Contains(shipment.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
