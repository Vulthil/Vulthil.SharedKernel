using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Respawn;

namespace Vulthil.SharedKernel.xUnit.Containers;

public class ContainerWrapper(IContainer container) : ICustomContainer
{
    public IContainer Container { get; } = container;

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
public class DatabaseContainer<TDbContext>(
    IDatabaseContainer container) : ContainerWrapper(container), ICustomDatabaseContainer
    where TDbContext : DbContext
{
    private readonly IDatabaseContainer _container = container;
    protected string ConnectionString => _container.GetConnectionString();

    public Type DbContextType => typeof(TDbContext);
    public bool HasBeenMigrated { get; private set; }

    public void MarkMigrated() => HasBeenMigrated = true;
}

public sealed class DatabaseContainerWithRespawner<TDbContext>(
    IDatabaseContainer container,
    RespawnerOptions respawnerOptions,
    Func<string, Task<DbConnection>> connectionFactory) : DatabaseContainer<TDbContext>(container), ICustomDatabaseContainerWithRespawner
    where TDbContext : DbContext
{
    private readonly RespawnerOptions _respawnerOptions = respawnerOptions;
    private readonly Func<string, Task<DbConnection>> _connectionFactory = connectionFactory;
    private Respawner? _respawner;

    public async Task ResetAsync()
    {
        if (_respawner is not null)
        {
            using var connection = await _connectionFactory(ConnectionString);
            await _respawner.ResetAsync(connection);
        }
    }

    public async Task InitializeRespawner()
    {
        if (_respawner is null)
        {
            using var connection = await _connectionFactory(ConnectionString);
            _respawner = await Respawner.CreateAsync(connection, _respawnerOptions);
        }
    }
}
