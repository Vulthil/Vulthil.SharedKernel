using System.Runtime.CompilerServices;

namespace Vulthil.SharedKernel.Primitives;

/// <summary>
/// Base class for domain entities identified by a strongly-typed identifier. Entities are compared by identity:
/// two entities are equal when they have the same runtime type and equal, non-default <see cref="Id"/> values.
/// A transient entity — one whose <see cref="Id"/> is still the default value — is only equal to itself.
/// </summary>
/// <typeparam name="TId">
/// The type of the entity identifier. It must provide value equality (for example a primitive, a <c>record</c>, or a
/// <c>readonly record struct</c>) for identity comparison to behave as intended.
/// </typeparam>
/// <param name="id">The unique identifier for this entity.</param>
public abstract class Entity<TId>(TId id)
    where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity. Set once during construction and used for equality comparisons.
    /// </summary>
    public TId Id { get; private set; } = id;

    /// <summary>
    /// Determines whether the specified object is an entity of the same runtime type with an equal, non-default
    /// identifier, or is the same reference.
    /// </summary>
    /// <param name="obj">The object to compare with the current entity.</param>
    /// <returns><see langword="true"/> if the objects are equal by identity; otherwise <see langword="false"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other || other.GetType() != GetType())
        {
            return false;
        }

        if (IsTransient() || other.IsTransient())
        {
            return ReferenceEquals(this, other);
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsTransient() ? RuntimeHelpers.GetHashCode(this) : EqualityComparer<TId>.Default.GetHashCode(Id);

    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default!);
}
