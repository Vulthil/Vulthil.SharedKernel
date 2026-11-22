namespace Shipments.Domain;

/// <summary>
/// The life cycle of a shipment. The order of the values is the order a shipment moves through them.
/// </summary>
public enum ShipmentStatus
{
    Created = 0,
    PickedUp = 1,
    InTransit = 2,
    Delivered = 3,
}
