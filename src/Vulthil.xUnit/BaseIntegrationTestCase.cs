using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Http;

namespace Vulthil.xUnit;

/// <summary>
/// Base class for integration tests that use a <see cref="BaseWebApplicationFactory{TEntryPoint}"/> with container-based infrastructure.
/// </summary>
/// <remarks>
/// Supply <typeparamref name="TFactory"/> as an <see cref="IClassFixture{TFixture}"/> (or collection fixture) so its
/// containers are started once and shared across the tests in that scope; database state is reset after each test.
/// All tests in the scope also share the fixture's test host, and application logs reach the currently running test
/// through the factory's TestContext-routed logger.
/// </remarks>
public abstract class BaseIntegrationTestCase<TFactory, TEntryPoint> : IAsyncLifetime
    where TFactory : BaseWebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    /// <summary>
    /// Gets a cancellation token scoped to the current test execution.
    /// </summary>
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly Lazy<WebApplicationFactory<TEntryPoint>> _lazyFactory;

    /// <summary>
    /// Gets the shared factory fixture providing the container infrastructure and the test host.
    /// </summary>
    protected TFactory FactoryFixture { get; }

    /// <summary>
    /// Gets the web application factory this test runs against, created on first access via <see cref="CreateFactory"/>.
    /// </summary>
    protected WebApplicationFactory<TEntryPoint> Factory => _lazyFactory.Value;

    /// <summary>
    /// Gets the test output helper passed to the constructor, or <see langword="null"/> if not provided. Application
    /// logs are routed to the running test automatically; the helper is for writing test output directly.
    /// </summary>
    protected ITestOutputHelper? TestOutputHelper { get; }

    private readonly object _gate = new();
    private AsyncServiceScope? _scope;

    /// <summary>
    /// Gets a scoped service provider resolved from the test application's root services.
    /// The scope is created on first access and disposed after each test.
    /// </summary>
    public IServiceProvider ScopedServices
    {
        get
        {
            lock (_gate)
            {
                _scope ??= Factory.Services.CreateAsyncScope();

                return _scope.Value.ServiceProvider;
            }
        }
    }

    private HttpClient? _client;
    /// <summary>
    /// Gets an <see cref="HttpClient"/> connected to the test server, created on first access.
    /// </summary>
    public HttpClient Client
    {
        get
        {
            lock (_gate)
            {
                return _client ??= Factory.CreateClient();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance with the shared web application factory and optional test output.
    /// </summary>
    /// <param name="factory">The factory fixture providing container infrastructure.</param>
    /// <param name="testOutputHelper">Optional output helper for writing test output directly.</param>
    protected BaseIntegrationTestCase(TFactory factory, ITestOutputHelper? testOutputHelper = null)
    {
        FactoryFixture = factory;
        TestOutputHelper = testOutputHelper;
        _lazyFactory = new(CreateFactory);
    }

    /// <summary>
    /// Creates the <see cref="WebApplicationFactory{TEntryPoint}"/> this test runs against. Returns the shared
    /// <see cref="FactoryFixture"/> by default, so every test in the class reuses one test host. Override to derive a
    /// per-test factory (for example <c>FactoryFixture.WithWebHostBuilder(...)</c>) when the tests need per-test host
    /// configuration; a derived factory is disposed automatically after each test.
    /// </summary>
    /// <returns>The factory the current test should run against.</returns>
    protected virtual WebApplicationFactory<TEntryPoint> CreateFactory() => FactoryFixture;

    /// <summary>
    /// Gets the HTTP mock registered for the named HTTP client <paramref name="name"/>, for configuring stubbed
    /// responses and inspecting captured requests. The mock is reset after each test.
    /// </summary>
    /// <param name="name">The logical name of the HTTP client registered via <c>AddHttpMock</c> on the factory.</param>
    /// <returns>The registered HTTP mock.</returns>
    protected IHttpMock HttpMock(string name) => FactoryFixture.GetHttpMock(name);

    /// <summary>
    /// Gets the HTTP mock registered for the typed client <typeparamref name="TClient"/>, for configuring stubbed
    /// responses and inspecting captured requests. The mock is reset after each test.
    /// </summary>
    /// <typeparam name="TClient">The typed client service type registered via <c>AddHttpMock</c> on the factory.</typeparam>
    /// <returns>The registered HTTP mock.</returns>
    protected IHttpMock HttpMock<TClient>()
        where TClient : class
        => FactoryFixture.GetHttpMock<TClient>();

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        try
        {
            await FactoryFixture.ResetAsync();
        }
        finally
        {
            await ResetScope();
            _client?.Dispose();
            if (_lazyFactory.IsValueCreated && !ReferenceEquals(_lazyFactory.Value, FactoryFixture))
            {
                await _lazyFactory.Value.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Disposes the current service scope, if one exists, so the next access to
    /// <see cref="ScopedServices"/> resolves a fresh scope.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async ValueTask ResetScope()
    {
        AsyncServiceScope? scope;
        lock (_gate)
        {
            scope = _scope;
            _scope = null;
        }

        await (scope?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await Initialize();
        await ResetScope();
    }

    /// <summary>
    /// Override to perform custom async initialization before each test.
    /// </summary>
    /// <returns>A task representing the initialization work.</returns>
    public virtual ValueTask Initialize() => ValueTask.CompletedTask;
}
