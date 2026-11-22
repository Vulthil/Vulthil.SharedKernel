namespace Storefront.Api;

public sealed record TrackingEventView(int Status, string Location, DateTimeOffset OccurredAtUtc)
{
    public string StatusName => ShipmentStatusNames.Of(Status);
}
