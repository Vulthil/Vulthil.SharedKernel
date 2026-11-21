using Vulthil.SharedKernel.Events;

namespace Orders.Domain.Orders.Events;

public sealed record OrderPlacedDomainEvent(OrderId OrderId) : IDomainEvent;
