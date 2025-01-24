using System.Collections.Concurrent;
using System.Data.Common;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Respawn;

namespace Vulthil.SharedKernel.xUnit.Containers;

public sealed class DatabaseContainer : IDatabaseContainer
{
    private readonly DotNet.Testcontainers.Containers.IDatabaseContainer _container;
    private readonly (RespawnerOptions RespawnerOptions, Func<string, Task<DbConnection>> ConnectionFactory) _respawnerOptions;
    private Respawner? _respawner;

    public Func<Action<DbContextOptionsBuilder>> OptionsAction { get; }
    public Type DbContextType { get; }

    public bool HasBeenMigrated { get; private set; }

    public DatabaseContainer(Type dbContextType, DotNet.Testcontainers.Containers.IDatabaseContainer container, Func<string, Action<DbContextOptionsBuilder>> optionsAction, (RespawnerOptions RespawnerOptions, Func<string, Task<DbConnection>> ConnectionFactory) respawnerOptions)
    {
        _container = container;
        _respawnerOptions = respawnerOptions;
        DbContextType = dbContextType;
        OptionsAction = () => optionsAction(_container.GetConnectionString());
    }

    public async Task ResetAsync()
    {
        if (_respawner is not null)
        {
            using var connection = await _respawnerOptions.ConnectionFactory(_container.GetConnectionString());
            await _respawner.ResetAsync(connection);
        }
    }

    public async Task InitializeRespawner()
    {
        if (_respawner is null)
        {
            var (respawnerOptions, connectionFactory) = _respawnerOptions;
            using var connection = await connectionFactory(_container.GetConnectionString());
            _respawner = await Respawner.CreateAsync(connection, respawnerOptions);
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

    protected abstract Func<string, Action<DbContextOptionsBuilder>> OptionsAction { get; }
    protected abstract (RespawnerOptions RespawnerOptions, Func<string, Task<DbConnection>> ConnectionFactory) RespawnerOptions { get; }

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

            _containerPool.Add(new DatabaseContainer(typeof(TDbContext), container, OptionsAction, RespawnerOptions));
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
