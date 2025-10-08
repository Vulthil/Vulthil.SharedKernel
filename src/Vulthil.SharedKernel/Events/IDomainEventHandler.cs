namespace Vulthil.SharedKernel.Events;

public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(TDomainEvent notification, CancellationToken cancellationToken = default);
}
