using Vulthil.SharedKernel.Events;

namespace WebApi.Domain.MainEntities.Events;

public sealed record MainEntityCreatedEvent(MainEntityId Id) : IDomainEvent;
