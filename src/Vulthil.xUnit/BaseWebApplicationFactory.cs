using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Containers;

namespace Vulthil.xUnit;

public abstract class BaseWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime
    where TEntryPoint : class
{
    private readonly HashSet<IContainerPool> _containerPools = [];

    private readonly Dictionary<IContainerPool, ICustomContainer> _containers = [];

    private IEnumerable<(IContainerWithConnectionStringPool Pool, ICustomContainer Container)> ContainersWithConnectionStrings => _containers
        .Where(x => x.Key is IContainerWithConnectionStringPool)
        .Select(x => ((IContainerWithConnectionStringPool)x.Key, x.Value));

    private IEnumerable<(IDatabaseContainerPool Pool, TContainerType Container)> DatabaseContainers<TContainerType>()
        where TContainerType : ICustomDatabaseContainer => _containers
        .Where(x => x.Key is IDatabaseContainerPool && x.Value is TContainerType)
        .Select(x => ((IDatabaseContainerPool)x.Key, (TContainerType)x.Value));
    protected BaseWebApplicationFactory(params IContainerPool[] containerPools) => Array.ForEach(containerPools, (p) => _containerPools.Add(p));

    protected void AddContainerPool(IContainerPool pool) => _containerPools.Add(pool);

    protected abstract void ConfigureContainers();

    public virtual async ValueTask InitializeAsync()
    {
        ConfigureContainers();
        foreach (var pool in _containerPools)
        {
            var container = await pool.GetContainerAsync();
            _containers.Add(pool, container);
        }
    }

    protected virtual void ConfigureCustomWebHost(IWebHostBuilder builder) { }

    protected override sealed void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (p, container) in ContainersWithConnectionStrings)
        {
            var connectionString = p.GetConnectionString(container.Container);
            builder.UseSetting($"ConnectionStrings:{p.KeyName}", connectionString);
        }

        ConfigureCustomWebHost(builder);
    }

    public sealed override async ValueTask DisposeAsync()
    {
        foreach (var (pool, container) in _containers)
        {
            pool.ReleaseContainer(container);
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    internal async Task ResetDatabase()
    {
        foreach (var (_, container) in DatabaseContainers<ICustomDatabaseContainerWithRespawner>())
        {
            await container.ResetAsync();
        }
    }

    internal async Task InitializeRespawners()
    {
        await using var scope = Services.CreateAsyncScope();
        foreach (var (pool, container) in DatabaseContainers<ICustomDatabaseContainer>())
        {
            await pool.ApplyMigrations(scope.ServiceProvider, container);
            if (container is ICustomDatabaseContainerWithRespawner containerWithRespawner)
            {
                await containerWithRespawner.InitializeRespawner();
            }
        }
    }
}
