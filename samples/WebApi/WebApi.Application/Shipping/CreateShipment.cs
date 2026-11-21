namespace WebApi.Application.Shipping;

/// <summary>
/// The command this service sends point-to-point to the Shipments sample's <see cref="QueueName"/> queue. The services
/// share no assembly: each declares its own record, and <see cref="Urn"/> is the wire identity both sides agree on.
/// </summary>
public sealed record CreateShipment(Guid OrderReference, string Recipient)
{
    public const string Urn = "urn:message:Samples:CreateShipment";

    public const string QueueName = "shipment-commands";
}
