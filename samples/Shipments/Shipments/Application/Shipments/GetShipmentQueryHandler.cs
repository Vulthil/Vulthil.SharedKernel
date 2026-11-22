using Microsoft.EntityFrameworkCore;
using Shipments.Domain;
using Shipments.Infrastructure;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Application.Shipments;

internal sealed class GetShipmentQueryHandler(ShipmentsDbContext dbContext) : IQueryHandler<GetShipmentQuery, Result<ShipmentDto>>
{
    public Task<Result<ShipmentDto>> HandleAsync(GetShipmentQuery request, CancellationToken cancellationToken = default)
    {
        var id = new ShipmentId(request.Id);
        return dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.TrackingEvents)
            .FirstOrDefaultAsync(shipment => shipment.Id == id, cancellationToken)
            .ToResultAsync(ShipmentErrors.NotFound(request.Id))
            .MapAsync(ShipmentDto.FromModel);
    }
}
