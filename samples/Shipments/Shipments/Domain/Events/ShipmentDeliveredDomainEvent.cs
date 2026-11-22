using Vulthil.SharedKernel.Events;

namespace Shipments.Domain.Events;

public sealed record ShipmentDeliveredDomainEvent(ShipmentId ShipmentId, DateTimeOffset DeliveredAtUtc) : IDomainEvent;
