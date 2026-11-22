using Shipments.Domain;

namespace Shipments.Application.Shipments;

public sealed record TrackingEventDto(ShipmentStatus Status, string Location, DateTimeOffset OccurredAtUtc);
