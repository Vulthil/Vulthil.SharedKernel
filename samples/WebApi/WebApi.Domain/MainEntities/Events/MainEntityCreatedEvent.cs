using Vulthil.SharedKernel.Events;

namespace WebApi.Domain.MainEntities.Events;

/// <summary>
/// Represents the MainEntityCreatedEvent.
/// </summary>
public sealed record MainEntityCreatedEvent(MainEntityId Id) : IDomainEvent;
