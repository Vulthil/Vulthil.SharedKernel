using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.xUnit.Containers;

namespace Vulthil.SharedKernel.xUnit;

public abstract class BaseWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime
    where TEntryPoint : class
{
    private readonly IDatabaseContainerPool[] _databaseContainerPools;

    private readonly Dictionary<IDatabaseContainerPool, IDatabaseContainer> _containers = [];

    protected BaseWebApplicationFactory(params IDatabaseContainerPool[] databaseContainerPools) => _databaseContainerPools = databaseContainerPools;

    public async ValueTask InitializeAsync()
    {
        foreach (var pool in _databaseContainerPools)
        {
            var container = await pool.GetContainerAsync();
            _containers.Add(pool, container);
        }
    }

    private static readonly MethodInfo AddDbContextMethod = typeof(BaseWebApplicationFactory<TEntryPoint>)
        .GetMethod(nameof(AddDbContext), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void AddDbContext<TDbContext>(IServiceCollection services, IDatabaseContainer container)
        where TDbContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TDbContext>>();

        services.AddDbContext<TDbContext>(container.OptionsAction);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            foreach (var (_, container) in _containers)
            {
                var genericAddDbContextMethod = AddDbContextMethod.MakeGenericMethod(container.DbContextType);
                genericAddDbContextMethod.Invoke(null, [services, container]);
            }
        });
    }

    public override async ValueTask DisposeAsync()
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
        foreach (var (_, container) in _containers)
        {
            await container.ResetAsync();
        }
    }

    internal async Task InitializeRespawners()
    {
        await using var scope = Services.CreateAsyncScope();
        foreach (var (pool, container) in _containers)
        {
            await pool.ApplyMigrations(scope.ServiceProvider, container);
            await container.InitializeRespawner();
        }
    }
}
