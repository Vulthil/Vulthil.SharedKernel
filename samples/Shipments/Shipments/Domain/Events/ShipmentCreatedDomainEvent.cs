using Vulthil.SharedKernel.Events;

namespace Shipments.Domain.Events;

public sealed record ShipmentCreatedDomainEvent(ShipmentId ShipmentId, Guid OrderReference) : IDomainEvent;
