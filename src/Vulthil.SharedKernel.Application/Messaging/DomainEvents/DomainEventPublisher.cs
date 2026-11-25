using System.Collections.Concurrent;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging.DomainEvents;

/// <summary>
/// The front door of domain-event dispatch: hands each event to the <see cref="DomainEventDispatcher{TEvent}"/>
/// of its runtime type, which owns the dispatch contract.
/// </summary>
internal sealed class DomainEventPublisher(IServiceProvider serviceProvider) : IDomainEventPublisher
{
    private static readonly ConcurrentDictionary<Type, IDomainEventDispatcher> _dispatchers = new();

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return DispatcherFor(domainEvent.GetType()).DispatchAsync(domainEvent, serviceProvider, cancellationToken);
    }

    private static IDomainEventDispatcher DispatcherFor(Type eventType) =>
        _dispatchers.GetOrAdd(eventType, static type =>
        {
            var dispatcherType = typeof(DomainEventDispatcher<>).MakeGenericType(type);
            return (IDomainEventDispatcher)(Activator.CreateInstance(dispatcherType)
                ?? throw new InvalidOperationException($"Could not create the dispatcher for domain event type {type}."));
        });
}
