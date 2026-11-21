namespace Shipments.Messages;

/// <summary>
/// The integration event published when a shipment is delivered. The Orders sample subscribes to it with a record of
/// its own: <see cref="Urn"/> and <see cref="Exchange"/> are the contract, and the exchange is a topic exchange routed
/// by <c>shipment.delivered.{carrier}</c>.
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
