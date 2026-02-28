using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Respawn;

namespace Vulthil.xUnit.Containers;

/// <summary>
/// Wraps a Testcontainers <see cref="IContainer"/> as a disposable <see cref="ICustomContainer"/>.
/// </summary>
/// <param name="container">The Testcontainers container to wrap.</param>
public class ContainerWrapper(IContainer container) : ICustomContainer
{
    /// <summary>
    /// Gets the underlying Testcontainers container instance.
    /// </summary>
    public IContainer Container { get; } = container;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
/// <summary>
/// Wraps a database container and tracks migration state for a specific <see cref="DbContext"/> type.
/// </summary>
public class DatabaseContainer<TDbContext>(
    IDatabaseContainer container) : ContainerWrapper(container), ICustomDatabaseContainer
    where TDbContext : DbContext
{
    private readonly IDatabaseContainer _container = container;
    /// <summary>
    /// Gets the connection string for the running database container.
    /// </summary>
    protected string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Gets the <see cref="Type"/> of the EF Core <see cref="DbContext"/> associated with this container.
    /// </summary>
    public Type DbContextType => typeof(TDbContext);
    /// <summary>
    /// Gets a value indicating whether EF Core migrations have been applied to this container's database.
    /// </summary>
    public bool HasBeenMigrated { get; private set; }

    /// <inheritdoc />
    public void MarkMigrated() => HasBeenMigrated = true;
}

/// <summary>
/// Extends <see cref="DatabaseContainer{TDbContext}"/> with Respawn-based database reset support.
/// </summary>
public sealed class DatabaseContainerWithRespawner<TDbContext>(
    IDatabaseContainer container,
    RespawnerOptions respawnerOptions,
    Func<string, Task<DbConnection>> connectionFactory) : DatabaseContainer<TDbContext>(container), ICustomDatabaseContainerWithRespawner
    where TDbContext : DbContext
{
    private readonly RespawnerOptions _respawnerOptions = respawnerOptions;
    private readonly Func<string, Task<DbConnection>> _connectionFactory = connectionFactory;
    private Respawner? _respawner;

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        if (_respawner is not null)
        {
            using var connection = await _connectionFactory(ConnectionString);
            await _respawner.ResetAsync(connection);
        }
    }

    /// <inheritdoc />
    public async Task InitializeRespawner()
    {
        if (_respawner is null)
        {
            using var connection = await _connectionFactory(ConnectionString);
            _respawner = await Respawner.CreateAsync(connection, _respawnerOptions);
        }
    }
}
