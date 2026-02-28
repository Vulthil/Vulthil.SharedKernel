using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Primitives;

/// <summary>
/// Base class for aggregate roots that track and raise domain events.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier for this aggregate root.</param>
    protected AggregateRoot(TId id) : base(id) { }

    private readonly List<IDomainEvent> _domainEvents = [];

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DomainEvents => [.. _domainEvents];

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Raises a domain event, adding it to the pending events collection.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    public void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
