using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit.Fixtures;
using Vulthil.xUnit.Http;

namespace Vulthil.xUnit;

/// <summary>
/// Abstract <see cref="WebApplicationFactory{TEntryPoint}"/> that consumes the shared containers of a
/// <see cref="ContainerHost"/> through per-factory scopes, injects their connection strings into the test host, and
/// ensures EF Core migrations are applied during host startup.
/// </summary>
/// <remarks>
/// <para>
/// Register the containers once on a <see cref="ContainerHost"/> assembly fixture and pass it to the constructor; the
/// factory then consumes every host container (filter with <see cref="ShouldUseContainer"/>) through a per-factory
/// scope view — an isolated database, virtual host, ... — so test classes run in parallel against shared containers
/// without interfering. Use a derived factory as an <see cref="IClassFixture{TFixture}"/> (or collection fixture) so
/// its scopes are provisioned once and shared across the tests in that scope, while
/// <see cref="BaseIntegrationTestCase{TEntryPoint}"/> resets database state between tests.
/// </para>
/// <para>
/// Migrations run from an <see cref="IHostedService"/> registered at the front of the host's hosted-service list, so the
/// schema exists before the application's own background services start. The migration step only applies migrations that
/// are still pending and tolerates a concurrent migrator, so an application that migrates itself on startup keeps
/// ownership and the factory never interferes with the application's own migration logic.
/// </para>
/// <para>
/// Every host the factory builds — its own and each one derived through <c>WithWebHostBuilder</c> — uses the same scope
/// views, so the same databases and virtual hosts. Only the newest live host runs its
/// <c>IRestartableHostedService</c>s: while a derived host runs, the hosts before it pause their restartable services
/// (for example the outbox relay and the message consumers), and they resume when the derived host stops.
/// </para>
/// </remarks>
public abstract class BaseWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime
    where TEntryPoint : class
{
    private readonly TestHostScope _scope;
    private readonly Dictionary<string, HttpMock> _httpMocks = [];
    private readonly List<Action<IServiceCollection>> _httpClientConfigurations = [];

    /// <summary>
    /// Initializes a factory that consumes the shared containers of <paramref name="containerHost"/>.
    /// </summary>
    /// <param name="containerHost">The assembly-level host whose containers this factory consumes.</param>
    protected BaseWebApplicationFactory(ContainerHost containerHost)
    {
        ArgumentNullException.ThrowIfNull(containerHost);
        _scope = new TestHostScope(containerHost, ShouldUseContainer, CreateScopeId, TimeProvider.System);
    }

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
    /// <c>AddHttpClient&lt;TClient, ...&gt;()</c>), keyed by the logical client name that <c>AddHttpClient</c> gives it,
    /// generic client types included. The implementation type does not need to be accessible.
    /// See <see cref="AddHttpMock(string)"/>.
    /// </summary>
    /// <typeparam name="TClient">The typed client service type registered with <c>AddHttpClient</c>.</typeparam>
    /// <returns>The registered mock; configure it per test with <see cref="GetHttpMock{TClient}()"/>, since a reset clears every rule.</returns>
    protected IHttpMock AddHttpMock<TClient>()
        where TClient : class
        => AddHttpMock(TypedClientName<TClient>());

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
        => GetHttpMock(TypedClientName<TClient>());

    // AddHttpClient<TClient>() derives the logical client name itself and writes generic arguments in C# syntax (such
    // as Client<int>), so asking it keeps the mock on the name that the real registration uses.
    private static string TypedClientName<TClient>()
        where TClient : class
        => new ServiceCollection().AddHttpClient<TClient>().Name;

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

        foreach (var (key, connectionString) in _scope.ConnectionStrings)
        {
            builder.UseSetting(key, connectionString);
        }

        foreach (var container in _scope.Containers)
        {
            container.ConfigureWebHost(builder);
        }

        builder.ConfigureTestServices(services =>
        {
            foreach (var container in _scope.Containers)
            {
                container.ConfigureServices(services);
            }
        });

        ConfigureCustomWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService>(_scope.CreateHostLifecycle));

            foreach (var configureHttpClient in _httpClientConfigurations)
            {
                configureHttpClient(services);
            }
        });
    }

    /// <summary>
    /// Starts the consumed host containers (once per test run, on the host) and provisions this factory's scope in
    /// each of them in parallel. Invoked once by xUnit before the tests in scope run.
    /// </summary>
    /// <returns>A task representing the asynchronous startup work.</returns>
    /// <exception cref="InvalidOperationException">Two consumed containers use the same connection string key.</exception>
    public async ValueTask InitializeAsync() => await _scope.InitializeAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        // Stop the application host(s) first so graceful shutdown still has its infrastructure, then tear down the
        // per-factory scopes. The containers themselves are owned by the ContainerHost and outlive the factory.
        await base.DisposeAsync().ConfigureAwait(false);

        await _scope.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the factory's scope between tests: pauses the restartable services of every running host built by this
    /// factory (its own and any derived through <c>WithWebHostBuilder</c>), resets the scope's resources, the HTTP
    /// mocks and the <see cref="Vulthil.Extensions.Testing.IResettableTestState"/>s of every live host, then resumes the
    /// paused services. Does nothing when no host is live, so a test that built no host never builds one just to reset
    /// it.
    /// </summary>
    /// <returns>A task that completes when every step has run.</returns>
    internal Task ResetAsync() => _scope.ResetAsync([.. _httpMocks.Values]);
}
