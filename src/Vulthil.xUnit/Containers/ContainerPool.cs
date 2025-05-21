using System.Collections.Concurrent;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Vulthil.xUnit.Containers;

public abstract class ContainerPool<TContainerType, TBuilderEntity, TContainerEntity> : IContainerPool, IAsyncLifetime
    where TContainerType : ICustomContainer
    where TContainerEntity : IContainer
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>
{

    private readonly int _poolSize;
    protected abstract int PoolSize { get; }

    private readonly ConcurrentBag<ICustomContainer> _containerPool = [];
    private readonly ConcurrentBag<ICustomContainer> _inUseContainers = [];

    private readonly SemaphoreSlim _semaphore;
    protected abstract IContainerBuilder<TBuilderEntity, TContainerEntity> ContainerBuilder { get; }

    protected ContainerPool()
    {
        _poolSize = PoolSize;
        _semaphore = new SemaphoreSlim(0, _poolSize);
    }

    public async Task<ICustomContainer> GetContainerAsync()
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

    public void ReleaseContainer(ICustomContainer container)
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

    protected abstract TContainerType CreateCustomContainer(TContainerEntity container);

    public async ValueTask InitializeAsync()
    {
        for (var i = 0; i < _poolSize; i++)
        {
            var container = ContainerBuilder
                .Build();
            await container.StartAsync();

            _containerPool.Add(CreateCustomContainer(container));

            _semaphore.Release();
        }
    }

}
