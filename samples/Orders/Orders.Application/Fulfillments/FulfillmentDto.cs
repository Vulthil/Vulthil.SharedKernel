using Orders.Domain.Fulfillments;

namespace Orders.Application.Fulfillments;

public sealed record FulfillmentDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public required FulfillmentStatus Status { get; init; }

    public static FulfillmentDto FromModel(Fulfillment fulfillment) => new()
    {
        Id = fulfillment.Id.Value,
        OrderId = fulfillment.OrderId,
        Status = fulfillment.Status
    };
}
