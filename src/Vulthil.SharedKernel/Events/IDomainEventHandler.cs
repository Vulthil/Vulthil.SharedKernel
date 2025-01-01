using MediatR;

namespace Vulthil.SharedKernel.Events;

public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
#pragma warning disable CA1033 // Interface methods should be callable by child types
    Task INotificationHandler<TDomainEvent>.Handle(TDomainEvent notification, CancellationToken cancellationToken) => HandleAsync(notification, cancellationToken);
#pragma warning restore CA1033 // Interface methods should be callable by child types
    Task HandleAsync(TDomainEvent notification, CancellationToken cancellationToken = default);
}
