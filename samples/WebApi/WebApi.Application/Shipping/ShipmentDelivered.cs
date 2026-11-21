namespace WebApi.Application.Shipping;

/// <summary>
/// This service's copy of the Shipments sample's integration event. <see cref="Urn"/> and <see cref="Exchange"/> are
/// the contract: the queue binds to that topic exchange with a <c>shipment.delivered.#</c> pattern.
/// </summary>
public sealed record ShipmentDelivered(
    Guid ShipmentId,
    Guid OrderReference,
    string Carrier,
    DateTimeOffset DeliveredAtUtc,
    int TrackingEventCount)
{
    public const string Urn = "urn:message:Samples:ShipmentDelivered";

    public const string Exchange = "samples.shipment-delivered";
}
