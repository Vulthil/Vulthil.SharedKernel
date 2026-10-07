using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Minimal aggregate root used by the provider outbox tests: creating one raises a domain event, which the outbox
/// capture interceptor persists as an <see cref="Vulthil.SharedKernel.Outbox.OutboxMessage"/> during
/// <c>SaveChangesAsync</c>.
/// </summary>
public sealed class OutboxProbe : AggregateRoot<Guid>
{
    public OutboxProbe(Guid id) : base(id)
    {
    }

    public static OutboxProbe Create() => Create(static id => new OutboxProbeCreated(id));

    /// <summary>
    /// Creates a probe that raises the domain event <paramref name="createdEvent"/> makes from the probe's ID, for a
    /// test that needs a domain event type of its own.
    /// </summary>
    public static OutboxProbe Create(Func<Guid, IDomainEvent> createdEvent)
    {
        var probe = new OutboxProbe(Guid.CreateVersion7());
        probe.Raise(createdEvent(probe.Id));
        return probe;
    }
}

/// <summary>
/// The domain event raised by <see cref="OutboxProbe.Create()"/>; its serialized form is what the relay dispatches.
/// </summary>
public sealed record OutboxProbeCreated(Guid ProbeId) : IDomainEvent;
