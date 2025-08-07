using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vulthil.xUnit;

public abstract class BaseIntegrationTestCase<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private readonly BaseWebApplicationFactory<TEntryPoint> _realFactory;
    private readonly Lazy<WebApplicationFactory<TEntryPoint>> _lazyFactory;
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

    protected BaseIntegrationTestCase(BaseWebApplicationFactory<TEntryPoint> factory, ITestOutputHelper? testOutputHelper = null)
    {
        _realFactory = factory;
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
        await _realFactory.ResetDatabase();
        await (_scope?.DisposeAsync() ?? ValueTask.CompletedTask);
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask ResetScope()
    {
        await (_scope?.DisposeAsync() ?? ValueTask.CompletedTask);
        _scope = null;
    }

    public ValueTask InitializeAsync() => Initialize();

    public virtual ValueTask Initialize() => ValueTask.CompletedTask;
}
