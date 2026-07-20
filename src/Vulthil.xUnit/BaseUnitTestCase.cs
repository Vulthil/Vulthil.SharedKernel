namespace Vulthil.xUnit;

/// <summary>
/// Base class for unit tests providing auto-mocking, lifecycle hooks, and a shared cancellation token.
/// </summary>
public abstract class BaseUnitTestCase : IAsyncLifetime
{
    /// <summary>
    /// Gets a cancellation token scoped to the current test execution.
    /// </summary>
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    /// <summary>
    /// Gets the auto-mocker used to create instances with automatically resolved mock dependencies.
    /// </summary>
    protected AutoMocker AutoMocker { get; } = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes every disposable instance the auto-mocker holds — explicit <see cref="Use{TService}(TService)"/>
    /// registrations, <see cref="UseReal{TService}"/>/<see cref="UseRealFor{TService, TImplementation}"/> instances
    /// once resolved, and auto-generated dependency mocks — then override (calling the base implementation) to
    /// perform further cleanup.
    /// </summary>
    /// <remarks>
    /// Only synchronous <see cref="IDisposable"/> instances are covered; an auto-mocker registration that is only
    /// <see cref="IAsyncDisposable"/> is not disposed by this sweep and needs an override to dispose it explicitly.
    /// </remarks>
    /// <returns>A task representing the cleanup work.</returns>
    protected virtual ValueTask Dispose()
    {
        AutoMocker.AsDisposable().Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask InitializeAsync() => Initialize();

    /// <summary>
    /// Override to perform custom async initialization before each test.
    /// </summary>
    /// <returns>A task representing the initialization work.</returns>
    protected virtual ValueTask Initialize() => ValueTask.CompletedTask;
    /// <summary>
    /// Retrieves the mock for the specified type from the auto-mocker.
    /// </summary>
    /// <typeparam name="TMock">The type to retrieve a mock for.</typeparam>
    /// <returns>The configured mock instance.</returns>
    protected virtual Mock<TMock> GetMock<TMock>()
        where TMock : class
        => AutoMocker.GetMock<TMock>();
    /// <summary>
    /// Registers an explicit service instance in the auto-mocker.
    /// </summary>
    /// <remarks>
    /// The auto-mocker takes ownership of <paramref name="service"/> for disposal purposes: if it implements
    /// <see cref="IDisposable"/>, it is disposed at test teardown along with every other auto-mocker-held instance
    /// (see <see cref="Dispose"/>).
    /// </remarks>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="service">The service instance to register.</param>
    protected virtual void Use<TService>(TService service)
        where TService : notnull
        => AutoMocker.Use(service);

    /// <summary>
    /// Configures the auto-mocker to create a real instance of <typeparamref name="TService"/> with all dependencies
    /// auto-mocked, resolved the first time <typeparamref name="TService"/> is requested and disposed automatically
    /// at teardown if disposable.
    /// </summary>
    /// <remarks>
    /// Unlike the eagerly-created <see cref="BaseUnitTestCase{TTarget}.Target"/>, creation is deferred until first
    /// resolution — directly via <see cref="Moq.AutoMock.AutoMocker.Get{TService}()"/>, or implicitly as another
    /// created instance's constructor dependency — so a dependency registered after this call but before that first
    /// resolution is still picked up.
    /// </remarks>
    /// <typeparam name="TService">The service type.</typeparam>
    protected virtual void UseReal<TService>()
        where TService : class
        => AutoMocker.Use<TService>(mocker => mocker.CreateInstance<TService>());

    /// <summary>
    /// Configures the auto-mocker to create a real instance of <typeparamref name="TImplementation"/> registered as
    /// <typeparamref name="TService"/>, resolved the first time <typeparamref name="TService"/> is requested and
    /// disposed automatically at teardown if disposable.
    /// </summary>
    /// <remarks>
    /// Unlike the eagerly-created <see cref="BaseUnitTestCase{TTarget}.Target"/>, creation is deferred until first
    /// resolution — directly via <see cref="Moq.AutoMock.AutoMocker.Get{TService}()"/>, or implicitly as another
    /// created instance's constructor dependency — so a dependency registered after this call but before that first
    /// resolution is still picked up.
    /// </remarks>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    protected virtual void UseRealFor<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
        => AutoMocker.Use<TService>(mocker => mocker.CreateInstance<TImplementation>());

    /// <summary>
    /// Creates an instance of <typeparamref name="TTarget"/> using the configured automocker.
    /// </summary>
    /// <typeparam name="TTarget">The target type to instantiate.</typeparam>
    /// <returns>A created instance of <typeparamref name="TTarget"/>.</returns>
    protected virtual TTarget CreateInstance<TTarget>() where TTarget : class => AutoMocker.CreateInstance<TTarget>();
}

/// <summary>
/// Base class for unit tests that automatically creates and exposes a lazily-initialized target instance of <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TTarget">The system-under-test type.</typeparam>
public abstract class BaseUnitTestCase<TTarget> : BaseUnitTestCase where TTarget : class
{
    private readonly Lazy<TTarget> _lazyTarget;
    /// <summary>
    /// Gets the lazily-initialized system-under-test instance, created via <see cref="BaseUnitTestCase.CreateInstance{TTarget}"/>.
    /// </summary>
    protected TTarget Target => _lazyTarget.Value;

    /// <summary>
    /// Initializes a new instance, deferring target creation to first access of <see cref="Target"/>.
    /// </summary>
    protected BaseUnitTestCase() => _lazyTarget = new(CreateInstance);

    /// <summary>
    /// Creates the target instance. Override to customize instantiation.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="TTarget"/>.</returns>
    protected virtual TTarget CreateInstance() => CreateInstance<TTarget>();

    /// <summary>
    /// Disposes <see cref="Target"/> first if it was created and is disposable, then defers to the base
    /// implementation to dispose everything the auto-mocker holds — so a disposable target is torn down before its
    /// dependencies are.
    /// </summary>
    /// <returns>A task representing the cleanup work.</returns>
    protected override async ValueTask Dispose()
    {
        if (_lazyTarget.IsValueCreated)
        {
            switch (_lazyTarget.Value)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }

        await base.Dispose();
    }
}

