using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Testing;

public sealed class BaseUnitTestCaseDisposalTests : BaseUnitTestCase<BaseUnitTestCaseDisposalTests.DisposableTarget>
{
    private bool _created;

    protected override DisposableTarget CreateInstance()
    {
        _created = true;
        return base.CreateInstance();
    }

    [Fact]
    public async Task DisposingAfterAccessingTargetDisposesItExactlyOnce()
    {
        // Arrange
        var target = Target;

        // Act
        await DisposeAsync();

        // Assert
        target.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposingWithoutEverAccessingTargetNeverCreatesIt()
    {
        // Act
        await DisposeAsync();

        // Assert
        _created.ShouldBeFalse();
    }

    public sealed class DisposableTarget : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}

public sealed class BaseUnitTestCaseAsyncDisposableTargetTests : BaseUnitTestCase<BaseUnitTestCaseAsyncDisposableTargetTests.AsyncDisposableTarget>
{
    [Fact]
    public async Task DisposingAfterAccessingTargetPrefersAsynchronousDisposal()
    {
        // Arrange
        var target = Target;

        // Act
        await DisposeAsync();

        // Assert
        target.DisposeCount.ShouldBe(1);
    }

    public sealed class AsyncDisposableTarget : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class UseRealAutoDisposalTests : BaseUnitTestCase<UseRealAutoDisposalTests.ConsumerOfRealService>
{
    public UseRealAutoDisposalTests() => UseReal<DisposableRealService>();

    [Fact]
    public async Task ADisposableRegisteredViaUseRealIsDisposedAtTeardownWithNoExplicitResolutionStep()
    {
        // Arrange — accessing Target is the only step; it pulls DisposableRealService in as a constructor
        // dependency, which is what resolves (and caches) the lazily-registered real instance.
        var consumer = Target;

        // Act
        await DisposeAsync();

        // Assert
        consumer.Service.DisposeCount.ShouldBe(1);
    }

    public sealed class DisposableRealService : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    public sealed class ConsumerOfRealService(DisposableRealService service)
    {
        public DisposableRealService Service => service;
    }
}

public sealed class UseRealForAutoDisposalTests : BaseUnitTestCase<UseRealForAutoDisposalTests.ConsumerOfRealService>
{
    public UseRealForAutoDisposalTests() => UseRealFor<IDisposableRealService, DisposableRealServiceImplementation>();

    [Fact]
    public async Task ADisposableRegisteredViaUseRealForIsDisposedAtTeardownWithNoExplicitResolutionStep()
    {
        // Arrange
        var consumer = Target;

        // Act
        await DisposeAsync();

        // Assert
        ((DisposableRealServiceImplementation)consumer.Service).DisposeCount.ShouldBe(1);
    }

    public interface IDisposableRealService;

    public sealed class DisposableRealServiceImplementation : IDisposableRealService, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    public sealed class ConsumerOfRealService(IDisposableRealService service)
    {
        public IDisposableRealService Service => service;
    }
}

public sealed class UseRealDependencyVisibilityTests : BaseUnitTestCase
{
    [Fact]
    public void ADependencyRegisteredAfterUseRealButBeforeFirstResolutionIsVisibleToItsConstruction()
    {
        // Arrange
        UseReal<ServiceWithADependency>();
        Use<INameProvider>(new NameProvider("late"));

        // Act
        var service = AutoMocker.Get<ServiceWithADependency>();

        // Assert
        service.ObservedName.ShouldBe("late");
    }

    public interface INameProvider
    {
        string Name { get; }
    }

    public sealed class NameProvider(string name) : INameProvider
    {
        public string Name => name;
    }

    public sealed class ServiceWithADependency(INameProvider nameProvider)
    {
        public string ObservedName => nameProvider.Name;
    }
}

public sealed class UseInstanceOwnershipTests : BaseUnitTestCase
{
    [Fact]
    public async Task AnExplicitlyRegisteredDisposableInstanceIsDisposedAtTeardown()
    {
        // Arrange
#pragma warning disable CA2000 // Ownership transfers to the auto-mocker; Use() takes over disposal at teardown.
        var service = new DisposableService();
#pragma warning restore CA2000
        Use(service);

        // Act
        await DisposeAsync();

        // Assert
        service.DisposeCount.ShouldBe(1);
    }

    public sealed class DisposableService : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
