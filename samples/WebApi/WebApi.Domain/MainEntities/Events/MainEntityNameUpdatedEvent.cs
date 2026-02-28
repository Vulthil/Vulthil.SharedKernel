using Vulthil.SharedKernel.Events;

namespace WebApi.Domain.MainEntities.Events;

/// <summary>
/// Represents the MainEntityNameUpdatedEvent.
/// </summary>
public sealed record MainEntityNameUpdatedEvent(MainEntityId Id, string Name) : IDomainEvent;
