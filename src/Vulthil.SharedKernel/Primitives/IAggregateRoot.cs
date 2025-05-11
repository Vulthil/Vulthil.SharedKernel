using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Primitives;

public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
