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

    private static Type GetOrAddMessageType(string typeName) => _typeCache.GetOrAdd(typeName, t =>
    {
        var type = Type.GetType(t);
        type ??= AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(t))
                .FirstOrDefault(found => found is not null);

        return type!;
    });
}
