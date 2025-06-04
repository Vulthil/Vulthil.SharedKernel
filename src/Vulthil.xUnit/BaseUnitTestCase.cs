
using Moq.AutoMock;

namespace Vulthil.xUnit;

public abstract class BaseUnitTestCase : IAsyncLifetime
{
    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    protected AutoMocker AutoMocker { get; } = new();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public ValueTask InitializeAsync() => Initialize();

    protected virtual ValueTask Initialize() => ValueTask.CompletedTask;
    protected virtual Mock<TMock> GetMock<TMock>()
        where TMock : class
        => AutoMocker.GetMock<TMock>();
    protected virtual void Use<TService>(TService service)
        where TService : notnull
        => AutoMocker.Use(service);

    protected virtual TTarget CreateInstance<TTarget>() where TTarget : class => AutoMocker.CreateInstance<TTarget>();
}

public abstract class BaseUnitTestCase<TTarget> : BaseUnitTestCase where TTarget : class
{
    private readonly Lazy<TTarget> _lazyTarget;
    protected TTarget Target => _lazyTarget.Value;

    protected BaseUnitTestCase() => _lazyTarget = new(CreateInstance);

    protected virtual TTarget CreateInstance() => CreateInstance<TTarget>();
}

