namespace Vulthil.SharedKernel.Primitives;

/// <summary>
/// Base class for domain entities identified by a strongly-typed identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
/// <param name="id">The unique identifier for this entity.</param>
public abstract class Entity<TId>(TId id)
    where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity. Set once during construction and used for equality comparisons.
    /// </summary>
    public TId Id { get; private set; } = id;
}
