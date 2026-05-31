using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit;

/// <summary>
/// Abstract <see cref="WebApplicationFactory{TEntryPoint}"/> that manages a set of test containers,
/// injects their connection strings into the test host, and ensures EF Core migrations are applied during host startup.
/// </summary>
/// <remarks>
/// Migrations run from an <see cref="IHostedService"/> registered at the front of the host's hosted-service list, so the
/// schema exists before the application's own background services start. The migration step only applies migrations that
/// are still pending and tolerates a concurrent migrator, so an application that migrates itself on startup keeps
/// ownership and the factory never interferes with the application's own migration logic.
/// Use a derived factory as an <see cref="IClassFixture{TFixture}"/> (or collection fixture) so its containers
/// are started once and shared across the tests in that scope, while
/// <see cref="BaseIntegrationTestCase{TFactory, TEntryPoint}"/> resets database state between tests.
/// </remarks>
public abstract class BaseWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime, ITestHostMigrator
    where TEntryPoint : class
{
    private bool _initialized;

    private readonly HashSet<ITestContainer> _containers = [];
    /// <summary>
    /// Gets the registered containers that expose a connection string, used for injecting settings into the test host.
    /// </summary>
    public IEnumerable<ITestContainerWithConnectionString> ContainersWithConnectionStrings => _containers
        .OfType<ITestContainerWithConnectionString>();
    /// <summary>
    /// Gets the registered database containers that support migrations and data resets.
    /// </summary>
    public IEnumerable<ITestDatabaseContainer> DatabaseContainers => _containers
        .OfType<ITestDatabaseContainer>();

    /// <summary>
    /// Registers a test container to be managed by this factory.
    /// </summary>
    /// <param name="container">The container to register.</param>
    protected void AddContainer(ITestContainer container) => _containers.Add(container);
    /// <summary>
    /// Override to register test containers by calling <see cref="AddContainer"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous registration work.</returns>
    protected virtual Task ConfigureContainers() => Task.CompletedTask;

    /// <summary>
    /// Override to apply additional <see cref="IWebHostBuilder"/> configuration for tests.
    /// </summary>
    /// <param name="builder">The web host builder to configure.</param>
    protected virtual void ConfigureCustomWebHost(IWebHostBuilder builder) { }
    /// <inheritdoc />
    protected override sealed void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var container in ContainersWithConnectionStrings)
        {
            var connectionString = container.ConnectionString;
            builder.UseSetting($"ConnectionStrings:{container.ConnectionStringKey}", connectionString);
        }

        builder.ConfigureServices(services => services.Insert(0, ServiceDescriptor.Singleton<IHostedService>(
                    sp => new TestMigrationHostedService(this, sp))));

        ConfigureCustomWebHost(builder);
    }

    /// <summary>
    /// Registers and starts every configured test container in parallel. Invoked once by xUnit before the tests in scope run.
    /// </summary>
    /// <returns>A task representing the asynchronous startup work.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        await ConfigureContainers();

        await Parallel.ForEachAsync(_containers, (container, ct) => container.InitializeAsync());
        _initialized = true;
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_initialized)
        {
            await Parallel.ForEachAsync(_containers, (container, ct) => container.DisposeAsync());
        }

        await base.DisposeAsync();
    }

    Task ITestHostMigrator.MigrateDatabases(IServiceProvider serviceProvider) =>
        Parallel.ForEachAsync(DatabaseContainers, (x, ct) => x.MigrateDatabase(serviceProvider));

    internal Task ResetDatabase() =>
        Parallel.ForEachAsync(DatabaseContainers, (x, ct) => x.ResetDatabase());
}

internal interface ITestHostMigrator
{
    Task MigrateDatabases(IServiceProvider serviceProvider);
}

internal sealed class TestMigrationHostedService(ITestHostMigrator migrator, IServiceProvider serviceProvider) : IHostedService
{
    private bool _completed;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        await migrator.MigrateDatabases(scope.ServiceProvider);
        _completed = true;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
