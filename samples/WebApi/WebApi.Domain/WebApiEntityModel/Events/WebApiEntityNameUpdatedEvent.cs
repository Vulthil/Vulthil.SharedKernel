using Vulthil.SharedKernel.Events;

namespace WebApi.Domain.WebApiEntityModel.Events;

public sealed record WebApiEntityNameUpdatedEvent(WebApiEntityId Id, string Name) : IDomainEvent;
