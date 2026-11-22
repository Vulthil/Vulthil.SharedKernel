namespace Shipments.Domain;

public sealed record ShipmentId(Guid Value)
{
    public static ShipmentId New() => new(Guid.CreateVersion7());
}
