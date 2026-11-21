namespace Shipments.Messages;

/// <summary>
/// The command the WebApi sample sends point-to-point to <see cref="QueueNames.ShipmentCommands"/>. The two services
/// share no assembly: each declares its own record, and <see cref="Urn"/> is the wire identity both sides agree on.
/// </summary>
public sealed record CreateShipment(Guid OrderReference, string Recipient)
{
    public const string Urn = "urn:message:Samples:CreateShipment";
}
