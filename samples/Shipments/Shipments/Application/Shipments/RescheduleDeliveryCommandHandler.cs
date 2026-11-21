using Microsoft.EntityFrameworkCore;
using Shipments.Domain;
using Shipments.Infrastructure;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Shipments.Application.Shipments;

internal sealed class RescheduleDeliveryCommandHandler(ShipmentsDbContext dbContext) : ICommandHandler<RescheduleDeliveryCommand>
{
    public Task<Result> HandleAsync(RescheduleDeliveryCommand request, CancellationToken cancellationToken = default)
    {
        var id = new ShipmentId(request.ShipmentId);
        return dbContext.Shipments
            .FirstOrDefaultAsync(shipment => shipment.Id == id, cancellationToken)
            .ToResultAsync(ShipmentErrors.NotFound(request.ShipmentId))
            .BindAsync(shipment => shipment.RescheduleDelivery(request.EstimatedDeliveryUtc))
            .TapAsync(() => dbContext.SaveChangesAsync(cancellationToken));
    }
}
