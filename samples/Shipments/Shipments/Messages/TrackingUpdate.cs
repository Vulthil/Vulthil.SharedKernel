namespace Shipments.Messages;

/// <summary>
/// A scan the carrier reports for a shipment. Each variant has an exchange of its own; the tracking queue subscribes
/// to all of them and one consumer of the base type handles every variant.
/// </summary>
public abstract record TrackingUpdate(Guid ShipmentId, string Location, DateTimeOffset OccurredAtUtc);

public sealed record ParcelPickedUp(Guid ShipmentId, string Location, DateTimeOffset OccurredAtUtc)
    : TrackingUpdate(ShipmentId, Location, OccurredAtUtc);

public sealed record ParcelInTransit(Guid ShipmentId, string Location, DateTimeOffset OccurredAtUtc)
    : TrackingUpdate(ShipmentId, Location, OccurredAtUtc);

public sealed record ParcelDelivered(Guid ShipmentId, string Location, DateTimeOffset OccurredAtUtc)
    : TrackingUpdate(ShipmentId, Location, OccurredAtUtc);

public static class TrackingUpdates
{
    public static TrackingUpdate PickedUp(Guid shipmentId, string location, DateTimeOffset occurredAtUtc) =>
        new ParcelPickedUp(shipmentId, location, occurredAtUtc);

    public static TrackingUpdate InTransit(Guid shipmentId, string location, DateTimeOffset occurredAtUtc) =>
        new ParcelInTransit(shipmentId, location, occurredAtUtc);

    public static TrackingUpdate Delivered(Guid shipmentId, string location, DateTimeOffset occurredAtUtc) =>
        new ParcelDelivered(shipmentId, location, occurredAtUtc);
}
