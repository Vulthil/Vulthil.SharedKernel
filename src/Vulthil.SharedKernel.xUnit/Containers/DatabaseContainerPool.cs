using System.Collections.Concurrent;
using System.Data.Common;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Respawn;

namespace Vulthil.SharedKernel.xUnit.Containers;

public sealed class DatabaseContainer<TDbContext> : IDatabaseContainer
    where TDbContext : DbContext
{
    private readonly DotNet.Testcontainers.Containers.IContainer _container;
    private readonly string _connectionString;
    private readonly RespawnerOptions _respawnerOptions;
    private readonly Func<string, Task<DbConnection>> _connectionFactory;
    private Respawner? _respawner;

    public Action<DbContextOptionsBuilder> OptionsAction { get; }
    public Type DbContextType => typeof(TDbContext);

    public bool HasBeenMigrated { get; private set; }

    public DatabaseContainer(DotNet.Testcontainers.Containers.IContainer container, string connectionString, Func<string, Action<DbContextOptionsBuilder>> optionsAction, RespawnerOptions respawnerOptions, Func<string, Task<DbConnection>> connectionFactory)
    {
        _container = container;
        _connectionString = connectionString;
        _respawnerOptions = respawnerOptions;
        _connectionFactory = connectionFactory;
        OptionsAction = optionsAction(_connectionString);
    }

    public async Task ResetAsync()
    {
        if (_respawner is not null)
        {
            using var connection = await _connectionFactory(_connectionString);
            await _respawner.ResetAsync(connection);
        }
    }

    public async Task InitializeRespawner()
    {
        if (_respawner is null)
        {
            using var connection = await _connectionFactory(_connectionString);
            _respawner = await Respawner.CreateAsync(connection, _respawnerOptions);
        }
    }

    public void MarkMigrated() => HasBeenMigrated = true;
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

public abstract class DatabaseContainerPool<TDbContext, TBuilderEntity, TContainerEntity> : IDatabaseContainerPool, IAsyncLifetime
    where TDbContext : DbContext
    where TContainerEntity : DotNet.Testcontainers.Containers.IDatabaseContainer
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>
{
    private readonly int _poolSize;
    protected abstract int PoolSize { get; }
    private readonly ConcurrentBag<IDatabaseContainer> _containerPool = [];
    private readonly SemaphoreSlim _semaphore;

    // Keep track of containers that are currently in use (for lifecycle management)
    private readonly ConcurrentBag<IDatabaseContainer> _inUseContainers = [];

    protected abstract IContainerBuilder<TBuilderEntity, TContainerEntity> ContainerBuilder { get; }

    protected abstract Action<DbContextOptionsBuilder> GetOptionsAction(string connectionString);
    protected abstract Task<DbConnection> GetOpenConnectionAsync(string connectionString);
    protected abstract RespawnerOptions RespawnerOptions { get; }

    protected DatabaseContainerPool()
    {
        _poolSize = PoolSize;
        _semaphore = new SemaphoreSlim(0, _poolSize);
    }

    public async ValueTask InitializeAsync()
    {
        for (var i = 0; i < _poolSize; i++)
        {
            var container = ContainerBuilder
                .Build();

            await container.StartAsync();

            _containerPool.Add(new DatabaseContainer<TDbContext>(container, container.GetConnectionString(), GetOptionsAction, RespawnerOptions, GetOpenConnectionAsync));
            _semaphore.Release();
        }
    }

    public async Task<IDatabaseContainer> GetContainerAsync()
    {
        await _semaphore.WaitAsync();

        if (_containerPool.TryTake(out var container))
        {
            _inUseContainers.Add(container);
            return container;
        }

        // This should not occur due to semaphore control
        throw new InvalidOperationException("No containers available in the pool.");
    }

    public void ReleaseContainer(IDatabaseContainer container)
    {
        if (_inUseContainers.Contains(container))
        {
            _inUseContainers.TryTake(out _);
            _containerPool.Add(container);
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var container in _containerPool.Concat(_inUseContainers))
        {
            await container.DisposeAsync();
        }
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    public abstract Task ApplyMigrations(IServiceProvider services, IDatabaseContainer container);
}
