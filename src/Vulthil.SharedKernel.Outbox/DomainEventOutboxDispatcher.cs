using System.Text.Json;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Relays <see cref="OutboxDestination.DomainEvent"/> rows to in-process domain-event handlers via
/// <see cref="IDomainEventPublisher"/>. Registered by default when outbox processing is enabled, preserving the
/// original in-process outbox behavior.
/// </summary>
internal sealed class DomainEventOutboxDispatcher(IDomainEventPublisher domainEventPublisher) : IOutboxDispatcher
{
    public bool Handles(OutboxDestination destination) => destination == OutboxDestination.DomainEvent;

    public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
    {
        var messageType = OutboxMessageTypeResolver.Resolve(message.Type, "domain-event");
        var domainEvent = JsonSerializer.Deserialize(message.Content, messageType)!;

        await domainEventPublisher.PublishAsync(domainEvent, cancellationToken);
    }
}
