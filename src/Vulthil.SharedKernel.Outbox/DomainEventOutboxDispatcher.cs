using System.Text.Json;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Events;

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
        var messageType = OutboxMessageTypes.Resolve(message.Type);
        if (JsonSerializer.Deserialize(message.Content, messageType) is not IDomainEvent domainEvent)
        {
            throw new InvalidOperationException(
                $"The outbox message {message.Id} of type '{message.Type}' is not a domain event: its content does not " +
                $"deserialize to an {nameof(IDomainEvent)}.");
        }

        await domainEventPublisher.PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
