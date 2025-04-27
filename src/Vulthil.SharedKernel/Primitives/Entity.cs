namespace Vulthil.SharedKernel.Primitives;

public abstract class Entity<TId>
    where TId : notnull
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

}
