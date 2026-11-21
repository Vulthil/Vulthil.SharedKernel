namespace Storefront.Api;

public sealed record OrderView(Guid Id, string Description, DateTimeOffset? ShippedAtUtc, IReadOnlyList<FulfillmentView> Fulfillments)
{
    public string FulfillmentStatus => Fulfillments.Count == 0 ? "Not started" : Fulfillments[0].StatusName;
}
