using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Domain.MainEntities;

/// <summary>
/// Represents the MainEntityId.
/// </summary>
public sealed record MainEntityId(Guid Value);

/// <summary>
/// Represents the MainEntity.
/// </summary>
public class MainEntity : AggregateRoot<MainEntityId>
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public string Name { get; private set; }
    private MainEntity(string name) : base(new(Guid.CreateVersion7())) => Name = name;
    /// <summary>
    /// Executes this member.
    /// </summary>
    public static MainEntity Create(string name)
    {
        var mainEntity = new MainEntity(name);
        mainEntity.Raise(new MainEntityCreatedEvent(mainEntity.Id));

        return mainEntity;
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    public void UpdateName(string name)
    {
        Name = name;
        Raise(new MainEntityNameUpdatedEvent(Id, Name));
    }
}

/// <summary>
/// Represents the MainEntityErrors.
/// </summary>
public static class MainEntityErrors
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public static Error NotFound(Guid id) => Error.NotFound("MainEntity.NotFound", $"Entity with Id {id} was not found.");
}
