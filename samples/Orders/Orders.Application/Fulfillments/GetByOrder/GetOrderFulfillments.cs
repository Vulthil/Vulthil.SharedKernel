namespace Orders.Application.Fulfillments.GetByOrder;

/// <summary>
/// A request/reply message answered by <see cref="OrderFulfillmentsRequestConsumer"/> with the fulfillments of one
/// order.
/// </summary>
public sealed record GetOrderFulfillments(Guid OrderId);
