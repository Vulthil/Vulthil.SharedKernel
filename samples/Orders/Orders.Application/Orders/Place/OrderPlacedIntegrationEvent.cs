namespace Orders.Application.Orders.Place;

/// <summary>
/// Published to the broker when an order is placed. This service consumes it itself to start the order's
/// fulfillment.
/// </summary>
public sealed record OrderPlacedIntegrationEvent(Guid OrderId);
