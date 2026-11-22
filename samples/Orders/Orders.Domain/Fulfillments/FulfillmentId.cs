namespace Orders.Domain.Fulfillments;

public sealed record FulfillmentId(Guid Value)
{
    public static FulfillmentId New() => new(Guid.CreateVersion7());
}
