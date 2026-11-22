using Vulthil.Results;

namespace Shipments.Domain;

public static class ShipmentErrors
{
    public static readonly Error RecipientRequired =
        Error.Validation("Shipment.RecipientRequired", "A shipment needs a recipient.");

    public static readonly Error OrderReferenceRequired =
        Error.Validation("Shipment.OrderReferenceRequired", "A shipment needs the reference of the order it ships.");

    public static readonly Error EstimateInThePast =
        Error.Validation("Shipment.EstimateInThePast", "The estimated delivery must be in the future.");

    public static readonly Error AlreadyDelivered =
        Error.Conflict("Shipment.AlreadyDelivered", "A delivered shipment cannot be rescheduled.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Shipment.NotFound", $"Shipment {id} was not found.");

    public static Error StatusOutOfOrder(ShipmentStatus current, ShipmentStatus next) =>
        Error.Conflict("Shipment.StatusOutOfOrder", $"A shipment that is {current} cannot move to {next}.");
}
