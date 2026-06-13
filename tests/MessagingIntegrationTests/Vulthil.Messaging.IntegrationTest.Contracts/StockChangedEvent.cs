namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record StockChangedEvent(Guid Id, string Sku, int Delta) : IInventoryEvent;
