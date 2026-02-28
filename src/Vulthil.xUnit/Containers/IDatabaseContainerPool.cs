
using DotNet.Testcontainers.Containers;

namespace Vulthil.xUnit.Containers;

/// <summary>
/// Manages a pool of reusable test containers.
/// </summary>
public interface IContainerPool
{
    /// <summary>
    /// Acquires a container from the pool, creating one if necessary.
    /// </summary>
    Task<ICustomContainer> GetContainerAsync();
    /// <summary>
    /// Returns a container to the pool for reuse.
    /// </summary>
    /// <param name="container">The container to release.</param>
    void ReleaseContainer(ICustomContainer container);
}

/// <summary>
/// Extends <see cref="IContainerPool"/> with connection string support for containers that expose network endpoints.
/// </summary>
public interface IContainerWithConnectionStringPool : IContainerPool
{
    /// <summary>
    /// Gets the configuration key name for the connection string (e.g., "MyDb").
    /// </summary>
    string KeyName { get; }

    /// <summary>
    /// Extracts the connection string from the given container.
    /// </summary>
    /// <param name="container">The container to extract the connection string from.</param>
    /// <returns>The connection string.</returns>
    string GetConnectionString(IContainer container);
}

/// <summary>
/// Strongly-typed variant of <see cref="IContainerWithConnectionStringPool"/> for a specific container type.
/// </summary>
/// <typeparam name="TContainerEntity">The container type.</typeparam>
public interface IContainerWithConnectionStringPool<TContainerEntity> : IContainerWithConnectionStringPool
    where TContainerEntity : IContainer
{
    /// <inheritdoc />
    string IContainerWithConnectionStringPool.GetConnectionString(IContainer container) => GetConnectionString((TContainerEntity)container);
    /// <summary>
    /// Extracts the connection string from the strongly-typed container.
    /// </summary>
    /// <param name="container">The container instance.</param>
    /// <returns>The connection string.</returns>
    string GetConnectionString(TContainerEntity container);
}

/// <summary>
/// Extends <see cref="IContainerWithConnectionStringPool"/> with EF Core migration support for database containers.
/// </summary>
public interface IDatabaseContainerPool : IContainerWithConnectionStringPool
{
    /// <summary>
    /// Applies EF Core migrations to the database in the specified container.
    /// </summary>
    /// <param name="services">The service provider to resolve the DbContext from.</param>
    /// <param name="container">The database container to migrate.</param>
    Task ApplyMigrations(IServiceProvider services, ICustomDatabaseContainer container);
}
/// <summary>
/// Strongly-typed variant of <see cref="IDatabaseContainerPool"/> for a specific container type.
/// </summary>
/// <typeparam name="TContainerEntity">The container type.</typeparam>
public interface IDatabaseContainerPool<TContainerEntity> : IDatabaseContainerPool, IContainerWithConnectionStringPool<TContainerEntity>
    where TContainerEntity : IContainer;
