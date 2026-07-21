using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, Type> _typeCache = [];

    public bool Handles(OutboxDestination destination) => destination == OutboxDestination.DomainEvent;

    public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
    {
        var messageType = GetOrAddMessageType(message.Type);
        var domainEvent = JsonSerializer.Deserialize(message.Content, messageType)!;

        await domainEventPublisher.PublishAsync(domainEvent, cancellationToken);
    }

    private static Type GetOrAddMessageType(string typeName) => _typeCache.GetOrAdd(typeName, static t =>
    {
        var type = Type.GetType(t)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(t))
                .FirstOrDefault(found => found is not null);

        return type ?? throw new InvalidOperationException(
            $"Unable to resolve the domain-event type '{t}' for an outbox relay. " +
            "Ensure the assembly that defines the type is loaded in the relay process.");
    });
}
