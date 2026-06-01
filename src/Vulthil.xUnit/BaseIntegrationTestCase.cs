using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit.Http;

namespace Vulthil.xUnit;

/// <summary>
/// Base class for integration tests that use a <see cref="BaseWebApplicationFactory{TEntryPoint}"/> with container-based infrastructure.
/// </summary>
/// <remarks>
/// Supply <typeparamref name="TFactory"/> as an <see cref="IClassFixture{TFixture}"/> (or collection fixture) so its
/// containers are started once and shared across the tests in that scope; database state is reset after each test.
/// </remarks>
public abstract class BaseIntegrationTestCase<TFactory, TEntryPoint> : IAsyncLifetime
    where TFactory : BaseWebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    /// <summary>
    /// Gets a cancellation token scoped to the current test execution.
    /// </summary>
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly TFactory _factory;
    private readonly Lazy<WebApplicationFactory<TEntryPoint>> _lazyFactory;

    /// <summary>
    /// Gets the lazily-initialized web application factory configured with test containers.
    /// </summary>
    protected WebApplicationFactory<TEntryPoint> Factory => _lazyFactory.Value;

    /// <summary>
    /// Gets the test output helper used to capture log output, or <see langword="null"/> if not provided.
    /// </summary>
    protected ITestOutputHelper? TestOutputHelper { get; }

    private AsyncServiceScope? _scope;

    /// <summary>
    /// Gets a scoped service provider resolved from the test application's root services.
    /// The scope is created on first access and disposed after each test.
    /// </summary>
    public IServiceProvider ScopedServices
    {
        get
        {
            _scope ??= Factory.Services.CreateAsyncScope();

            return _scope.Value.ServiceProvider;
        }
    }

    private HttpClient? _client;
    /// <summary>
    /// Gets an <see cref="HttpClient"/> connected to the test server, created on first access.
    /// </summary>
    public HttpClient Client => _client ??= Factory.CreateClient();

    /// <summary>
    /// Initializes a new instance with the shared web application factory and optional test output.
    /// </summary>
    /// <param name="factory">The factory fixture providing container infrastructure.</param>
    /// <param name="testOutputHelper">Optional output helper for capturing log output.</param>
    protected BaseIntegrationTestCase(TFactory factory, ITestOutputHelper? testOutputHelper = null)
    {
        _factory = factory;
        TestOutputHelper = testOutputHelper;
        _lazyFactory = new(CreateFactory);
    }

    private WebApplicationFactory<TEntryPoint> CreateFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            if (TestOutputHelper is not null)
            {
                builder.ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.Services.AddSingleton<ILoggerProvider>(serviceProvider => new XUnitLoggerProvider(TestOutputHelper, new XUnitLoggerOptions()
                    {
                        IncludeCategory = true,
                        IncludeLogLevel = true,
                        IncludeScopes = true,
                    }));
                });
            }
        });

    /// <summary>
    /// Gets the HTTP mock registered for the named HTTP client <paramref name="name"/>, for configuring stubbed
    /// responses and inspecting captured requests. The mock is reset after each test.
    /// </summary>
    /// <param name="name">The logical name of the HTTP client registered via <c>AddHttpMock</c> on the factory.</param>
    /// <returns>The registered HTTP mock.</returns>
    protected IHttpMock HttpMock(string name) => _factory.GetHttpMock(name);

    /// <summary>
    /// Gets the HTTP mock registered for the typed client <typeparamref name="TClient"/>, for configuring stubbed
    /// responses and inspecting captured requests. The mock is reset after each test.
    /// </summary>
    /// <typeparam name="TClient">The typed client service type registered via <c>AddHttpMock</c> on the factory.</typeparam>
    /// <returns>The registered HTTP mock.</returns>
    protected IHttpMock HttpMock<TClient>()
        where TClient : class
        => _factory.GetHttpMock<TClient>();

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        await _factory.ResetAsync();
        await ResetScope();
        _client?.Dispose();
        if (_lazyFactory.IsValueCreated)
        {
            await _lazyFactory.Value.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the current service scope, if one exists, so the next access to
    /// <see cref="ScopedServices"/> resolves a fresh scope.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async ValueTask ResetScope()
    {
        await (_scope?.DisposeAsync() ?? ValueTask.CompletedTask);
        _scope = null;
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
