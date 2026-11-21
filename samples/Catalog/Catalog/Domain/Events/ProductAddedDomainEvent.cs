using Vulthil.SharedKernel.Events;

namespace Catalog.Domain.Events;

public sealed record ProductAddedDomainEvent(ProductId ProductId) : IDomainEvent;
