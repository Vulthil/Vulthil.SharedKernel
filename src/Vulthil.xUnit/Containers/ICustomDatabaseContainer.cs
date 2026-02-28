using DotNet.Testcontainers.Containers;

namespace Vulthil.xUnit.Containers;

/// <summary>
/// Wraps a Testcontainers container instance for test infrastructure use.
/// </summary>
public interface ICustomContainer : IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying Testcontainers container instance.
    /// </summary>
    IContainer Container { get; }
}

/// <summary>
/// Extends <see cref="ICustomContainer"/> with database-specific migration tracking.
/// </summary>
public interface ICustomDatabaseContainer : ICustomContainer
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the EF Core DbContext associated with this database container.
    /// </summary>
    Type DbContextType { get; }
    /// <summary>
    /// Gets a value indicating whether EF Core migrations have been applied to this container's database.
    /// </summary>
    bool HasBeenMigrated { get; }

    /// <summary>
    /// Marks this container as having had its migrations applied.
    /// </summary>
    void MarkMigrated();
}

/// <summary>
/// Extends <see cref="ICustomDatabaseContainer"/> with Respawn-based database reset support.
/// </summary>
public interface ICustomDatabaseContainerWithRespawner : ICustomDatabaseContainer
{
    /// <summary>
    /// Creates and caches the Respawner instance for this database.
    /// </summary>
    Task InitializeRespawner();
    /// <summary>
    /// Resets the database to a clean state using the cached Respawner.
    /// </summary>
    Task ResetAsync();
}
