namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Abstract fixture that manages a set of test containers, starting them in parallel during initialization
/// and providing database migration and reset support.
/// </summary>
public abstract class TestFixture : IAsyncLifetime
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
    /// Registers a test container to be managed by this fixture.
    /// </summary>
    /// <param name="container">The container to register.</param>
    protected void AddContainer(ITestContainer container) => _containers.Add(container);
    /// <summary>
    /// Override to register test containers by calling <see cref="AddContainer"/>.
    /// </summary>
    protected abstract Task ConfigureContainers();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (!_initialized)
        {
            return;
        }

        await Parallel.ForEachAsync(_containers, (container, ct) => container.DisposeAsync());
    }

    /// <inheritdoc />
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

    internal Task MigrateDatabases(IServiceProvider serviceProvider) =>
        Parallel.ForEachAsync(DatabaseContainers, (x, ct) => x.MigrateDatabase(serviceProvider));
    internal Task InitializeRespawners() =>
        Parallel.ForEachAsync(DatabaseContainers, (x, ct) => x.InitializeRespawner());
    internal Task ResetDatabase() =>
        Parallel.ForEachAsync(DatabaseContainers, (x, ct) => x.ResetDatabase());
}
