using Vulthil.SharedKernel.Events;

namespace WebApi.Domain.MainEntities.Events;

public sealed record MainEntityNameUpdatedEvent(MainEntityId Id, string Name) : IDomainEvent;
