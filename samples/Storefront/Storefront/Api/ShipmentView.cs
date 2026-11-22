namespace Storefront.Api;

public sealed record ShipmentView(
    Guid Id,
    Guid OrderReference,
    string Recipient,
    string Carrier,
    int Status,
    DateTimeOffset? EstimatedDeliveryUtc,
    DateTimeOffset? DeliveredAtUtc,
    IReadOnlyList<TrackingEventView> TrackingEvents)
{
    public string StatusName => ShipmentStatusNames.Of(Status);
}
