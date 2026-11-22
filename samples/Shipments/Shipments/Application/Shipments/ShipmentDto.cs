using Shipments.Domain;

namespace Shipments.Application.Shipments;

public sealed record ShipmentDto(
    Guid Id,
    Guid OrderReference,
    string Recipient,
    string Carrier,
    ShipmentStatus Status,
    DateTimeOffset? EstimatedDeliveryUtc,
    DateTimeOffset? DeliveredAtUtc,
    IReadOnlyList<TrackingEventDto> TrackingEvents)
{
    public static ShipmentDto FromModel(Shipment shipment) => new(
        shipment.Id.Value,
        shipment.OrderReference,
        shipment.Recipient,
        shipment.Carrier,
        shipment.Status,
        shipment.EstimatedDeliveryUtc,
        shipment.DeliveredAtUtc,
        [.. shipment.TrackingEvents
            .OrderBy(trackingEvent => trackingEvent.OccurredAtUtc)
            .Select(trackingEvent => new TrackingEventDto(trackingEvent.Status, trackingEvent.Location, trackingEvent.OccurredAtUtc))]);
}
