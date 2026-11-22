using Vulthil.SharedKernel.Events;

namespace Orders.Domain.Orders.Events;

public sealed record OrderDescriptionChangedDomainEvent(OrderId OrderId, string Description) : IDomainEvent;
