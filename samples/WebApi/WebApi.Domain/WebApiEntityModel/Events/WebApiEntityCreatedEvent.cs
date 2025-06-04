using Vulthil.SharedKernel.Events;

namespace WebApi.Domain.WebApiEntityModel.Events;

public sealed record WebApiEntityCreatedEvent(WebApiEntityId Id) : IDomainEvent;
