using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.xUnit.Http;

namespace Vulthil.xUnit;

/// <summary>
/// Base class for integration tests that run against a <see cref="BaseWebApplicationFactory{TEntryPoint}"/> with
/// container-based infrastructure. Builds on <see cref="BaseUnitTestCase"/>, so a test registers doubles the same way
/// a unit test does: any service registered on the test case — <see cref="BaseUnitTestCase.Use{TService}"/>,
/// <see cref="BaseUnitTestCase.GetMock{TMock}"/>, <see cref="BaseUnitTestCase.UseReal{TService}"/> or
/// <see cref="BaseUnitTestCase.UseRealFor{TService, TImplementation}"/> — before the host is first touched replaces
/// that service in a per-test copy of the host, so an external dependency is mocked with one line.
/// </summary>
/// <remarks>
/// Supply the factory as an <see cref="IClassFixture{TFixture}"/> (or collection fixture) so its containers are
/// started once and shared across the tests in that scope; database state is reset after each test. Tests that
/// register no services share the fixture's test host; a test that registers services runs on a derived host built
/// through <see cref="WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/>, disposed after the test. Application
/// logs reach the currently running test through the factory's TestContext-routed logger.
/// </remarks>
/// <typeparam name="TEntryPoint">The application's entry point type, typically <c>Program</c>.</typeparam>
public abstract class BaseIntegrationTestCase<TEntryPoint> : BaseUnitTestCase
    where TEntryPoint : class
{
    private readonly Lazy<WebApplicationFactory<TEntryPoint>> _lazyFactory;
    private readonly HashSet<Type> _testCaseServices = [];
    private readonly object _gate = new();
    private AsyncServiceScope? _scope;
    private HttpClient? _client;

    /// <summary>
    /// Initializes a new instance with the shared web application factory and optional test output.
    /// </summary>
    /// <param name="factory">The factory fixture providing the container infrastructure and the test host.</param>
    /// <param name="testOutputHelper">Optional output helper for writing test output directly.</param>
    protected BaseIntegrationTestCase(BaseWebApplicationFactory<TEntryPoint> factory, ITestOutputHelper? testOutputHelper = null)
    {
        ArgumentNullException.ThrowIfNull(factory);

        FactoryFixture = factory;
        TestOutputHelper = testOutputHelper;
        _lazyFactory = new(CreateFactory);
    }

    /// <summary>
    /// Gets the shared factory fixture providing the container infrastructure and the test host.
    /// </summary>
    protected BaseWebApplicationFactory<TEntryPoint> FactoryFixture { get; }

    /// <summary>
    /// Gets the web application factory this test runs against, created on first access via <see cref="CreateFactory"/>.
    /// </summary>
    protected WebApplicationFactory<TEntryPoint> Factory => _lazyFactory.Value;

    /// <summary>
    /// Gets the test output helper passed to the constructor, or <see langword="null"/> if not provided. Application
    /// logs are routed to the running test automatically; the helper is for writing test output directly.
    /// </summary>
    protected ITestOutputHelper? TestOutputHelper { get; }

    /// <summary>
    /// Gets a scoped service provider resolved from the test application's root services.
    /// The scope is created on first access and disposed after each test.
    /// </summary>
    protected IServiceProvider ScopedServices
    {
        get
        {
            // Building the host runs the application's entry point on another thread, which calls back into this
            // instance through ConfigureTestCaseServices; holding _gate across that build would deadlock it.
            var hostServices = Factory.Services;
            lock (_gate)
            {
                _scope ??= hostServices.CreateAsyncScope();

                return _scope.Value.ServiceProvider;
            }
        }
    }

    /// <summary>
    /// Gets an <see cref="HttpClient"/> connected to the test server, created on first access.
    /// </summary>
    protected HttpClient Client
    {
        get
        {
            lock (_gate)
            {
                if (_client is not null)
                {
                    return _client;
                }
            }

            // Same as ScopedServices: creating the client builds the host, so it must happen outside _gate.
            var client = Factory.CreateClient();
            lock (_gate)
            {
                if (_client is null)
                {
                    _client = client;
                }
                else
                {
                    client.Dispose();
                }

                return _client;
            }
        }
    }

    /// <summary>
    /// Creates the <see cref="WebApplicationFactory{TEntryPoint}"/> this test runs against. By default this is the
    /// shared <see cref="FactoryFixture"/>, so every test in the class reuses one test host — unless the test
    /// registered services on the test case first, in which case it is a per-test derived factory whose host has
    /// those services swapped in (see <see cref="ConfigureTestCaseServices"/>). Override to derive a per-test factory
    /// yourself (for example <c>FactoryFixture.WithWebHostBuilder(...)</c>) when the tests need other per-test host
    /// configuration; call <see cref="ConfigureTestCaseServices"/> from the builder's <c>ConfigureTestServices</c> to
    /// keep the registered doubles. A derived factory is disposed automatically after each test, and the post-test
    /// reset always targets whichever factory this method returns, never an unrelated, never-built host.
    /// </summary>
    /// <returns>The factory the current test should run against.</returns>
    protected virtual WebApplicationFactory<TEntryPoint> CreateFactory()
    {
        var serviceTypes = SnapshotTestCaseServices();
        return serviceTypes.Length == 0
            ? FactoryFixture
            : FactoryFixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => ReplaceServices(services, serviceTypes)));
    }

    /// <summary>
    /// Replaces, in <paramref name="services"/>, every service registered on this test case with the instance the
    /// auto-mocker holds for it: the explicit <see cref="BaseUnitTestCase.Use{TService}"/> instance, the mock from
    /// <see cref="BaseUnitTestCase.GetMock{TMock}"/>, or the real instance from
    /// <see cref="BaseUnitTestCase.UseReal{TService}"/>. Register under the service type the application resolves
    /// (<c>Use&lt;IWeatherClient&gt;(stub)</c>, not <c>Use(stub)</c>), since the replacement is keyed by that type.
    /// The default <see cref="CreateFactory"/> applies this for you; an override applies it from its own
    /// <c>ConfigureTestServices</c>.
    /// </summary>
    /// <param name="services">The test host's service collection.</param>
    protected void ConfigureTestCaseServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ReplaceServices(services, SnapshotTestCaseServices());
    }

    private void ReplaceServices(IServiceCollection services, Type[] serviceTypes)
    {
        foreach (var serviceType in serviceTypes)
        {
            services.RemoveAll(serviceType);
            services.AddSingleton(serviceType, AutoMocker.Get(serviceType));
        }
    }

    private Type[] SnapshotTestCaseServices()
    {
        lock (_gate)
        {
            return [.. _testCaseServices];
        }
    }

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
    /// <remarks>The registration also swaps <typeparamref name="TService"/> into this test's host; see <see cref="ConfigureTestCaseServices"/>.</remarks>
    protected override void Use<TService>(TService service)
    {
        RegisterTestCaseService(typeof(TService));
        base.Use(service);
    }

    /// <inheritdoc />
    /// <remarks>The mock also replaces <typeparamref name="TMock"/> in this test's host; see <see cref="ConfigureTestCaseServices"/>.</remarks>
    protected override Mock<TMock> GetMock<TMock>()
    {
        RegisterTestCaseService(typeof(TMock));
        return base.GetMock<TMock>();
    }

    /// <inheritdoc />
    /// <remarks>The real instance also replaces <typeparamref name="TService"/> in this test's host; see <see cref="ConfigureTestCaseServices"/>.</remarks>
    protected override void UseReal<TService>()
    {
        RegisterTestCaseService(typeof(TService));
        base.UseReal<TService>();
    }

    /// <inheritdoc />
    /// <remarks>The real instance also replaces <typeparamref name="TService"/> in this test's host; see <see cref="ConfigureTestCaseServices"/>.</remarks>
    protected override void UseRealFor<TService, TImplementation>()
    {
        RegisterTestCaseService(typeof(TService));
        base.UseRealFor<TService, TImplementation>();
    }

    /// <summary>
    /// Runs the <see cref="BaseUnitTestCase.Initialize"/> hook, then discards the service scope it may have used so
    /// the test itself starts from a fresh one.
    /// </summary>
    /// <returns>A task representing the initialization work.</returns>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        await ResetScope().ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the host this test ran on (restartable services paused, resettable resources cleared), disposes the
    /// scope, the client and any per-test derived factory, then disposes everything the auto-mocker holds. Override
    /// (calling the base implementation) for further cleanup.
    /// </summary>
    /// <returns>A task representing the cleanup work.</returns>
    protected override async ValueTask Dispose()
    {
        try
        {
            // Only the factory this test actually ran on (Factory, e.g. a WithWebHostBuilder(...) clone of
            // FactoryFixture) has the running host whose restartable services need pausing around the reset; a test
            // that never touched Factory never built any host, so there is nothing to reset.
            if (_lazyFactory.IsValueCreated)
            {
                await FactoryFixture.ResetAsync(_lazyFactory.Value.Services).ConfigureAwait(false);
            }
        }
        finally
        {
            await ResetScope().ConfigureAwait(false);
            _client?.Dispose();
            if (_lazyFactory.IsValueCreated && !ReferenceEquals(_lazyFactory.Value, FactoryFixture))
            {
                await _lazyFactory.Value.DisposeAsync().ConfigureAwait(false);
            }

            await base.Dispose().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes the current service scope, if one exists, so the next access to
    /// <see cref="ScopedServices"/> resolves a fresh scope.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    protected async ValueTask ResetScope()
    {
        AsyncServiceScope? scope;
        lock (_gate)
        {
            scope = _scope;
            _scope = null;
        }

        if (scope is not null)
        {
            await scope.Value.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void RegisterTestCaseService(Type serviceType)
    {
        lock (_gate)
        {
            // A service registered once the host is built can no longer be swapped in; failing loudly beats a test
            // that silently runs against the real service. Re-registering (or re-fetching a mock) is fine.
            if (_testCaseServices.Add(serviceType) && _lazyFactory.IsValueCreated)
            {
                throw new InvalidOperationException(
                    $"'{serviceType.Name}' was registered after this test's host was built. Register test-case services before first touching Factory, Client or ScopedServices.");
            }
        }
    }
}
