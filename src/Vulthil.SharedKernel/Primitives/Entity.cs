using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Primitives;

public abstract class Entity<TId> : IEntity
    where TId : class
{
    public TId Id { get; private set; }

    protected Entity(TId id)
        : this() => Id = id;

    /// <summary>
    /// EF Core Only
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected Entity()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
    }

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
