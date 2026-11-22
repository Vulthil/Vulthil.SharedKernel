namespace Shipments.Domain;

public sealed record TrackingEventId(Guid Value)
{
    public static TrackingEventId New() => new(Guid.CreateVersion7());
}
