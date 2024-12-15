using MediatR;

namespace Vulthil.SharedKernel.Events;

public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    new Task Handle(TDomainEvent notification, CancellationToken cancellationToken = default);
    Task INotificationHandler<TDomainEvent>.Handle(TDomainEvent notification, CancellationToken cancellationToken) => Handle(notification, cancellationToken);
}