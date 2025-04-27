using MediatR;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IDomainEventPublisher
{
    Task PublishAsync(object notification, CancellationToken cancellationToken = default);
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : IDomainEvent;
}


internal sealed class DomainEventPublisher(IPublisher publisher) : IDomainEventPublisher
{
    private readonly IPublisher _publisher = publisher;

    public Task PublishAsync(object notification, CancellationToken cancellationToken = default) =>
        _publisher.Publish(notification, cancellationToken);
    public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : IDomainEvent =>
        _publisher.Publish(notification, cancellationToken);
}
