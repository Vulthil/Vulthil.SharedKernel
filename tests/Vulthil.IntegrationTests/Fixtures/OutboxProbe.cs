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

    public static OutboxProbe Create()
    {
        var probe = new OutboxProbe(Guid.CreateVersion7());
        probe.RaiseCreated();
        return probe;
    }

    private void RaiseCreated() => Raise(new OutboxProbeCreated(Id));
}

/// <summary>
/// The domain event raised by <see cref="OutboxProbe.Create"/>; its serialized form is what the relay dispatches.
/// </summary>
public sealed record OutboxProbeCreated(Guid ProbeId) : IDomainEvent;
