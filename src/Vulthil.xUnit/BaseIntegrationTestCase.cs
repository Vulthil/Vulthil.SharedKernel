using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit;

public abstract class BaseIntegrationTestCase<TFactory, TEntryPoint> : IAsyncLifetime
    where TFactory : BaseWebApplicationFactory<TEntryPoint>, new()
    where TEntryPoint : class
{
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly TFactory _realFactory;
    private readonly Lazy<WebApplicationFactory<TEntryPoint>> _lazyFactory;

    private readonly TestFixture _testFixture;
    protected WebApplicationFactory<TEntryPoint> Factory => _lazyFactory.Value;

    protected ITestOutputHelper? TestOutputHelper { get; }

    private AsyncServiceScope? _scope;

    public IServiceProvider ScopedServices
    {
        get
        {
            _scope ??= Factory.Services.CreateAsyncScope();

            return _scope.Value.ServiceProvider;
        }
    }

    private HttpClient? _client;
    public HttpClient Client => _client ??= Factory.CreateClient();

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

    public virtual async ValueTask DisposeAsync()
    {
        await _testFixture.ResetDatabase();
        await ResetScope();
        _client?.Dispose();
        await _realFactory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public async ValueTask ResetScope()
    {
        await (_scope?.DisposeAsync() ?? ValueTask.CompletedTask);
        _scope = null;
    }

    public async ValueTask InitializeAsync()
    {
        await _testFixture.MigrateDatabases(ScopedServices);
        await _testFixture.InitializeRespawners();
        await Initialize();
        await ResetScope();
    }

    public virtual ValueTask Initialize() => ValueTask.CompletedTask;
}
