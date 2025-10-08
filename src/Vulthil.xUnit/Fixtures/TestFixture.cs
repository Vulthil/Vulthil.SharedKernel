namespace Vulthil.xUnit.Fixtures;

public abstract class TestFixture : IAsyncLifetime
{
    private bool _initialized;

    private readonly HashSet<ITestContainer> _containers = [];
    public IEnumerable<ITestContainerWithConnectionString> ContainersWithConnectionStrings => _containers
        .OfType<ITestContainerWithConnectionString>();
    public IEnumerable<ITestDatabaseContainer> DatabaseContainers => _containers
        .OfType<ITestDatabaseContainer>();

    protected void AddContainer(ITestContainer container) => _containers.Add(container);
    protected abstract Task ConfigureContainers();

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (!_initialized)
        {
            return;
        }

        await Parallel.ForEachAsync(_containers, (container, ct) => container.DisposeAsync());
    }

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
