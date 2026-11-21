using Orders.Application.Fulfillments;
using Orders.Domain.Orders;

namespace Orders.Application.Orders;

public sealed record OrderDto(Guid Id, string Description, DateTimeOffset? ShippedAtUtc, IReadOnlyList<FulfillmentDto> Fulfillments)
{
    internal static OrderDto FromModel(Order order, IReadOnlyList<FulfillmentDto> fulfillments) =>
        new(order.Id.Value, order.Description, order.ShippedAtUtc, fulfillments);
}
