using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Primitives;

/// <summary>
/// Defines the contract for an aggregate root that tracks domain events.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets a snapshot of the pending domain events raised by this aggregate.
    /// Events are cleared after they have been dispatched.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Removes all pending domain events from this aggregate.
    /// </summary>
    void ClearDomainEvents();
}
