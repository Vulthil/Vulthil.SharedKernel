using Vulthil.SharedKernel.Exceptions;

namespace Shipments.Domain;

/// <summary>
/// Thrown when a message refers to a shipment that does not exist. It carries
/// <see cref="ShipmentErrors.NotFound(Guid)"/>, so the problem-details handler answers it with a 404 when it escapes
/// an HTTP request, and the tracking queue ignores it for retries because waiting does not make the shipment exist.
/// </summary>
public sealed class ShipmentNotFoundException(Guid shipmentId) : DomainException(ShipmentErrors.NotFound(shipmentId))
{
    public Guid ShipmentId { get; } = shipmentId;
}
