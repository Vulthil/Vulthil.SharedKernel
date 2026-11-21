using Vulthil.SharedKernel.Primitives;

namespace Shipments.Domain;

/// <summary>
/// A child entity of <see cref="Shipment"/>: it has an identity of its own, but it is only created and changed
/// through its aggregate root.
/// </summary>
public sealed class TrackingEvent : Entity<TrackingEventId>
{
    private TrackingEvent(TrackingEventId id, ShipmentStatus status, string location, DateTimeOffset occurredAtUtc)
        : base(id)
    {
        Status = status;
        Location = location;
        OccurredAtUtc = occurredAtUtc;
    }

    public ShipmentStatus Status { get; private set; }

    public string Location { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static TrackingEvent Create(ShipmentStatus status, string location, DateTimeOffset occurredAtUtc) =>
        new(TrackingEventId.New(), status, location, occurredAtUtc);
}
