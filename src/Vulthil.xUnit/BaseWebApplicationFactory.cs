using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Vulthil.Extensions.Hosting;
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
/// To share containers across many parallel test classes, register them on a <see cref="ContainerHost"/> assembly
/// fixture instead and pass it to the <see cref="BaseWebApplicationFactory{TEntryPoint}(ContainerHost)"/> constructor;
/// the factory then consumes every host container through a per-factory scope view (an isolated database, virtual
/// host, ...) so test classes run in parallel against shared containers without interfering.
/// </remarks>
public abstract class BaseWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime, ITestHostMigrator
    where TEntryPoint : class
{
    private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(30);

    private bool _initialized;

    private readonly ContainerHost? _containerHost;
    private readonly HashSet<ITestContainer> _containers = [];
    private readonly Dictionary<string, HttpMock> _httpMocks = [];
    private readonly List<Action<IServiceCollection>> _httpClientConfigurations = [];

    /// <summary>
    /// Initializes a factory that owns its containers exclusively; register them with <see cref="AddContainer"/> in
    /// the constructor or in <see cref="ConfigureContainers"/>.
    /// </summary>
    protected BaseWebApplicationFactory()
    {
    }

    /// <summary>
    /// Initializes a factory that consumes the shared containers of <paramref name="containerHost"/>. Every container
    /// registered on the host is consumed through a per-factory scope view (filter with
    /// <see cref="ShouldUseContainer"/>), so no per-factory container registration is needed and parallel test
    /// classes share the running containers without sharing state.
    /// </summary>
    /// <param name="containerHost">The assembly-level host whose containers this factory consumes.</param>
    protected BaseWebApplicationFactory(ContainerHost containerHost)
    {
        ArgumentNullException.ThrowIfNull(containerHost);
        _containerHost = containerHost;
    }
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

    private IEnumerable<IStartupResource> StartupResources => _containers.OfType<IStartupResource>();

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
    /// <returns>The registered mock; configure it per test with <see cref="GetHttpMock(string)"/>, since a reset clears every rule.</returns>
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
    /// <returns>The registered mock; configure it per test with <see cref="GetHttpMock{TClient}()"/>, since a reset clears every rule.</returns>
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
    /// Decides whether a container registered on the <see cref="ContainerHost"/> is consumed by this factory. All
    /// host containers are consumed by default; override to exclude containers a factory's scenario does not use
    /// (excluded containers are not started by this factory either, thanks to lazy host startup).
    /// </summary>
    /// <param name="container">A container registered on the host.</param>
    /// <returns><see langword="true"/> to consume the container; otherwise <see langword="false"/>.</returns>
    protected virtual bool ShouldUseContainer(ITestContainer container) => true;

    /// <summary>
    /// Creates the identifier under which this factory's scopes are isolated inside shared containers (database
    /// names, virtual hosts, ...). The default combines the factory type name with a random suffix, yielding a new
    /// unique scope per factory instance — that is, per test class when the factory is used as a class fixture.
    /// </summary>
    /// <returns>A short, unique, lowercase identifier that is safe to embed in names.</returns>
    protected virtual string CreateScopeId()
    {
        var name = new string([.. GetType().Name.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant)]);
        name = name.Length switch
        {
            0 => "scope",
            > 24 => name[..24],
            _ => name,
        };

        return $"{name}_{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Override to apply additional <see cref="IWebHostBuilder"/> configuration for tests.
    /// </summary>
    /// <param name="builder">The web host builder to configure.</param>
    protected virtual void ConfigureCustomWebHost(IWebHostBuilder builder) { }
    /// <inheritdoc />
    protected override sealed void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Route application logs to the test that is currently running; TestContext.Current resolves the right
        // output helper dynamically, so one shared host serves every test in the class without rebuilding it.
        builder.ConfigureLogging(logging => logging.Services.AddSingleton<ILoggerProvider>(
            new XUnitLoggerProvider(new XUnitLoggerOptions
            {
                IncludeCategory = true,
                IncludeLogLevel = true,
                IncludeScopes = true,
            })));

        foreach (var container in ContainersWithConnectionStrings)
        {
            var connectionString = container.ConnectionString;
            builder.UseSetting($"ConnectionStrings:{container.ConnectionStringKey}", connectionString);
        }

        foreach (var container in _containers)
        {
            container.ConfigureWebHost(builder);
        }

        builder.ConfigureTestServices(services =>
        {
            foreach (var container in _containers)
            {
                container.ConfigureServices(services);
            }
        });

        ConfigureCustomWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService>(
                sp => new TestMigrationHostedService(this, sp)));

            foreach (var configureHttpClient in _httpClientConfigurations)
            {
                configureHttpClient(services);
            }
        });
    }

    /// <summary>
    /// Registers and initializes every consumed test container in parallel. Invoked once by xUnit before the tests in
    /// scope run. Factory-owned containers are started here; containers consumed from a <see cref="ContainerHost"/>
    /// are started on the host (once per test run) and only their per-factory scopes are provisioned here.
    /// </summary>
    /// <returns>A task representing the asynchronous startup work.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        await ConfigureContainers();
        await AcquireHostContainers();

        await Parallel.ForEachAsync(_containers, (container, ct) => container.InitializeAsync());
        _initialized = true;
    }

    private async Task AcquireHostContainers()
    {
        if (_containerHost is null)
        {
            return;
        }

        var consumedContainers = _containerHost.Containers.Where(ShouldUseContainer).ToList();
        await Parallel.ForEachAsync(consumedContainers, async (container, ct) => await _containerHost.EnsureStartedAsync(container));

        var scopeId = CreateScopeId();
        foreach (var container in consumedContainers)
        {
#pragma warning disable CA2000 // Ownership transfers to _containers; scope views are disposed in DisposeAsync.
            AddContainer(CreateScopeView(container, scopeId));
#pragma warning restore CA2000
        }
    }

    private static ITestContainer CreateScopeView(ITestContainer container, string scopeId) => container switch
    {
        ITestContainerScopeProvider scopeProvider => scopeProvider.CreateScope(scopeId),
        ITestContainerWithConnectionString withConnectionString => new TestContainerWithConnectionStringScope(withConnectionString),
        _ => new TestContainerScope(container),
    };

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        // Stop the application host(s) first so graceful shutdown still has its infrastructure, then tear down
        // factory-owned containers and per-factory scopes. Containers owned by a ContainerHost outlive the factory.
        await base.DisposeAsync();

        await Parallel.ForEachAsync(_containers, (container, ct) => container.DisposeAsync());
    }

    async Task ITestHostMigrator.PrepareAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await Parallel.ForEachAsync(DatabaseContainers, (container, ct) => container.MigrateDatabase(scope.ServiceProvider));
        await Parallel.ForEachAsync(StartupResources, (resource, ct) => resource.InitializeAsync(serviceProvider));
    }

    /// <summary>
    /// Resets the given host's restartable services and this factory's registered resources. Accepts the host's
    /// service provider explicitly so a caller running against a derived factory (for example one produced by
    /// <c>WithWebHostBuilder(...)</c>) can pass that host's own <see cref="IServiceProvider"/> — resetting always
    /// targets the host the caller actually ran the test against, never an unrelated, never-built host.
    /// </summary>
    /// <param name="hostServices">The service provider of the host the test ran against.</param>
    /// <param name="cancellationToken">
    /// A token observed in addition to the per-service <see cref="RestartTimeout"/> bound, so a canceled test run
    /// unwinds the restart dance instead of always waiting out the full timeout.
    /// </param>
    internal async Task ResetAsync(IServiceProvider hostServices, CancellationToken cancellationToken)
    {
        var restartableServices = hostServices.GetServices<IHostedService>().OfType<IRestartableHostedService>().ToList();

        foreach (var service in restartableServices)
        {
            using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stopCts.CancelAfter(RestartTimeout);
            await service.StopAsync(stopCts.Token);
        }

        try
        {
            await ResetResourcesAsync(hostServices);
        }
        finally
        {
            foreach (var service in restartableServices)
            {
                using var startCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                startCts.CancelAfter(RestartTimeout);
                await service.StartAsync(startCts.Token);
            }
        }
    }

    private Task ResetResourcesAsync(IServiceProvider serviceProvider) =>
        Parallel.ForEachAsync(ResettableResources, (resource, ct) => resource.ResetAsync(serviceProvider));
}

internal interface ITestHostMigrator
{
    Task PrepareAsync(IServiceProvider serviceProvider);
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

        await migrator.PrepareAsync(serviceProvider);
        _completed = true;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
