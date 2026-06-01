using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Vulthil.xUnit.Fixtures;
using Vulthil.xUnit.Http;

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
    private readonly Dictionary<string, HttpMock> _httpMocks = [];
    private readonly List<Action<IServiceCollection>> _httpClientConfigurations = [];
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

    private IEnumerable<IResettableResource> ResettableResources => _containers
        .OfType<IResettableResource>()
        .Concat(_httpMocks.Values);

    /// <summary>
    /// Registers a test container to be managed by this factory.
    /// </summary>
    /// <param name="container">The container to register.</param>
    protected void AddContainer(ITestContainer container) => _containers.Add(container);

    /// <summary>
    /// Registers an in-process HTTP mock for the named <see cref="HttpClient"/> registered with
    /// <c>AddHttpClient("<paramref name="name"/>")</c>, and routes that client's outbound calls through it by replacing
    /// its primary message handler. Configure responses per test via <see cref="GetHttpMock(string)"/> (or
    /// <c>HttpMock("<paramref name="name"/>")</c> on the test case); the mock is reset between tests.
    /// </summary>
    /// <param name="name">The logical name of the HTTP client to mock.</param>
    /// <returns>The registered mock, so responses can optionally be configured up front.</returns>
    protected IHttpMock AddHttpMock(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var mock = new HttpMock();
        _httpMocks[name] = mock;

        _httpClientConfigurations.Add(services => services.Configure<HttpClientFactoryOptions>(
            name,
            options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = mock.CreateHandler())));

        return mock;
    }

    /// <summary>
    /// Registers an in-process HTTP mock for the typed client <typeparamref name="TClient"/> (as registered with
    /// <c>AddHttpClient&lt;TClient, ...&gt;()</c>), keyed by its logical client name. The implementation type does not
    /// need to be accessible. See <see cref="AddHttpMock(string)"/>.
    /// </summary>
    /// <typeparam name="TClient">The typed client service type registered with <c>AddHttpClient</c>.</typeparam>
    /// <returns>The registered mock, so responses can optionally be configured up front.</returns>
    protected IHttpMock AddHttpMock<TClient>()
        where TClient : class
        => AddHttpMock(typeof(TClient).Name);

    /// <summary>
    /// Gets the HTTP mock registered for the named HTTP client <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The logical name of the HTTP client.</param>
    /// <returns>The registered mock.</returns>
    /// <exception cref="InvalidOperationException">No mock was registered for <paramref name="name"/>.</exception>
    public IHttpMock GetHttpMock(string name)
        => _httpMocks.TryGetValue(name, out var mock)
            ? mock
            : throw new InvalidOperationException(
                $"No HTTP mock registered for client '{name}'. Call AddHttpMock(\"{name}\") in the factory.");

    /// <summary>
    /// Gets the HTTP mock registered for the typed client <typeparamref name="TClient"/>.
    /// </summary>
    /// <typeparam name="TClient">The typed client service type.</typeparam>
    /// <returns>The registered mock.</returns>
    /// <exception cref="InvalidOperationException">No mock was registered for <typeparamref name="TClient"/>.</exception>
    public IHttpMock GetHttpMock<TClient>()
        where TClient : class
        => GetHttpMock(typeof(TClient).Name);
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

        builder.ConfigureServices(services =>
        {
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService>(
                sp => new TestMigrationHostedService(this, sp)));

            foreach (var configureHttpClient in _httpClientConfigurations)
            {
                configureHttpClient(services);
            }
        });

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

    internal Task ResetAsync() =>
        Parallel.ForEachAsync(ResettableResources, (resource, ct) => resource.ResetAsync());
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
