using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit;

/// <summary>
/// Base class for integration tests that use a <see cref="WebApplicationFactory{TEntryPoint}"/> with container-based infrastructure.
/// </summary>
public abstract class BaseIntegrationTestCase<TFactory, TEntryPoint> : IAsyncLifetime
    where TFactory : BaseWebApplicationFactory<TEntryPoint>, new()
    where TEntryPoint : class
{
    /// <summary>
    /// Gets a cancellation token scoped to the current test execution.
    /// </summary>
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly TFactory _realFactory;
    private readonly Lazy<WebApplicationFactory<TEntryPoint>> _lazyFactory;

    private readonly TestFixture _testFixture;
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
    /// Initializes a new instance with the specified test fixture and optional test output.
    /// </summary>
    /// <param name="testFixture">The fixture providing container infrastructure.</param>
    /// <param name="testOutputHelper">Optional output helper for capturing log output.</param>
    protected BaseIntegrationTestCase(TestFixture testFixture, ITestOutputHelper? testOutputHelper = null)
    {
        _realFactory = new TFactory();
        _realFactory.SetFixture(testFixture);
        _testFixture = testFixture;
        TestOutputHelper = testOutputHelper;
        _lazyFactory = new(CreateFactory);
    }

    private WebApplicationFactory<TEntryPoint> CreateFactory() =>
        _realFactory.WithWebHostBuilder(builder =>
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

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        await _testFixture.ResetDatabase();
        await ResetScope();
        _client?.Dispose();
        await _realFactory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
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
