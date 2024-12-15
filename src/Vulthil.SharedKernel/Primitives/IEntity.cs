using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Primitives;

public interface IEntity
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
