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
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
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
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="service">The service instance to register.</param>
    protected virtual void Use<TService>(TService service)
        where TService : notnull
        => AutoMocker.Use(service);

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
}

