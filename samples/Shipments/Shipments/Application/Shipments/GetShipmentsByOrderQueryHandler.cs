using Microsoft.EntityFrameworkCore;
using Shipments.Infrastructure;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Application.Shipments;

internal sealed class GetShipmentsByOrderQueryHandler(ShipmentsDbContext dbContext)
    : IQueryHandler<GetShipmentsByOrderQuery, Result<IReadOnlyList<ShipmentDto>>>
{
    public async Task<Result<IReadOnlyList<ShipmentDto>>> HandleAsync(GetShipmentsByOrderQuery request, CancellationToken cancellationToken = default)
    {
        var shipments = await dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.TrackingEvents)
            .Where(shipment => shipment.OrderReference == request.OrderReference)
            .OrderBy(shipment => shipment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ShipmentDto>>([.. shipments.Select(ShipmentDto.FromModel)]);
    }
}
