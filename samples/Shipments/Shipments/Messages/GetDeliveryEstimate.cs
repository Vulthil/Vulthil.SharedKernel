namespace Shipments.Messages;

/// <summary>
/// A request/reply call to the carrier, answered with a <see cref="DeliveryEstimate"/>.
/// </summary>
public sealed record GetDeliveryEstimate(string Recipient);
