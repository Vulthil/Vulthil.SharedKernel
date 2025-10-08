using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Domain.MainEntities;

public sealed record MainEntityId(Guid Value);

public class MainEntity : AggregateRoot<MainEntityId>
{
    public string Name { get; private set; }
    private MainEntity(string name) : base(new(Guid.CreateVersion7())) => Name = name;
    public static MainEntity Create(string name)
    {
        var mainEntity = new MainEntity(name);
        mainEntity.Raise(new MainEntityCreatedEvent(mainEntity.Id));

        return mainEntity;
    }

    public void UpdateName(string name)
    {
        Name = name;
        Raise(new MainEntityNameUpdatedEvent(Id, Name));
    }
}

public static class MainEntityErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("MainEntity.NotFound", $"Entity with Id {id} was not found.");
}
