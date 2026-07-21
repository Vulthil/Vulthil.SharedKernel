namespace Vulthil.xUnit.Tests;

public sealed class TargetCreationTimingTests : BaseUnitTestCase<TargetCreationTimingTests.Marker>
{
    private int _creationCount;

    protected override Marker CreateInstance()
    {
        _creationCount++;
        return base.CreateInstance();
    }

    [Fact]
    public void AccessingTargetMultipleTimesCreatesItOnlyOnce()
    {
        // Act
        _ = Target;
        _ = Target;
        _ = Target;

        // Assert
        _creationCount.ShouldBe(1);
    }

    public sealed class Marker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}

public sealed class CreateInstanceHelperTests : BaseUnitTestCase
{
    [Fact]
    public void EachCallProducesAFreshInstanceUnlikeTheCachedTarget()
    {
        // Act
        var first = CreateInstance<Marker>();
        var second = CreateInstance<Marker>();

        // Assert
        first.ShouldNotBeSameAs(second);
    }

    public sealed class Marker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}

public sealed class GetMockConfigurationTests : BaseUnitTestCase<GetMockConfigurationTests.Consumer>
{
    [Fact]
    public void ConfiguringGetMockBeforeFirstTargetAccessIsObservedByTheCreatedTarget()
    {
        // Arrange
        GetMock<IGreeter>().Setup(greeter => greeter.Greet()).Returns("configured");

        // Act
        var greeting = Target.Greet();

        // Assert
        greeting.ShouldBe("configured");
    }

    public interface IGreeter
    {
        string Greet();
    }

    public sealed class Consumer(IGreeter greeter)
    {
        public string Greet() => greeter.Greet();
    }
}

public sealed class UseRegistrationTests : BaseUnitTestCase
{
    [Fact]
    public void UseRegistersTheInstanceOnTheUnderlyingAutoMocker()
    {
        // Arrange
        var service = new FakeService();

        // Act
        Use<IFakeService>(service);

        // Assert
        AutoMocker.Get<IFakeService>().ShouldBeSameAs(service);
    }

    public interface IFakeService;

    public sealed class FakeService : IFakeService;
}

public sealed class InitializeHookTests : BaseUnitTestCase
{
    private bool _initializeCalled;

    protected override ValueTask Initialize()
    {
        _initializeCalled = true;
        return base.Initialize();
    }

    [Fact]
    public async Task InitializeAsyncInvokesTheInitializeHook()
    {
        // Act
        await InitializeAsync();

        // Assert
        _initializeCalled.ShouldBeTrue();
    }
}

public sealed class DisposeHookTests : BaseUnitTestCase
{
    private bool _disposeCalled;

    protected override ValueTask Dispose()
    {
        _disposeCalled = true;
        return base.Dispose();
    }

    [Fact]
    public async Task DisposeAsyncInvokesTheDisposeHook()
    {
        // Act
        await DisposeAsync();

        // Assert
        _disposeCalled.ShouldBeTrue();
    }
}

public sealed class TargetDisposedBeforeAutoMockerDependenciesTests
    : BaseUnitTestCase<TargetDisposedBeforeAutoMockerDependenciesTests.Consumer>
{
    private readonly RecordingDependency _dependency = new();

    public TargetDisposedBeforeAutoMockerDependenciesTests() => Use<IDependency>(_dependency);

    [Fact]
    public async Task TargetIsDisposedBeforeAutoMockerHeldDependenciesAreSwept()
    {
        // Arrange
        var target = Target;

        // Act
        await DisposeAsync();

        // Assert
        target.DependencyWasAlreadyDisposedWhenTargetWasDisposed.ShouldBeFalse();
        _dependency.DisposeCount.ShouldBe(1);
    }

    public interface IDependency : IDisposable;

    public sealed class RecordingDependency : IDependency
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    public sealed class Consumer(IDependency dependency) : IDisposable
    {
        public bool DependencyWasAlreadyDisposedWhenTargetWasDisposed { get; private set; }

        public void Dispose() =>
            DependencyWasAlreadyDisposedWhenTargetWasDisposed = ((RecordingDependency)dependency).DisposeCount > 0;
    }
}
