using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace Vulthil.xUnit.Tests.Fixtures;

public sealed class ContainerHostTests : BaseUnitTestCase<ContainerHostTests.TestableContainerHost>
{
    protected override TestableContainerHost CreateInstance() => new(GetMock<IMessageSink>().Object);

    [Fact]
    public async Task InitializeAsyncRunsConfigureContainersExactlyOnceEvenWhenCalledRepeatedly()
    {
        // Act
        await Target.InitializeAsync();
        await Target.InitializeAsync();
        await Target.InitializeAsync();

        // Assert
        Target.ConfigureContainersCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ContainersReflectsEveryContainerRegisteredDuringConfiguration()
    {
        // Arrange
#pragma warning disable CA2000 // Ownership transfers to the host under test; its own DisposeAsync is what tears these down.
        var first = new FakeTestContainer();
        var second = new FakeTestContainer();
#pragma warning restore CA2000
        Target.QueueContainer(first);
        Target.QueueContainer(second);

        // Act
        await Target.InitializeAsync();

        // Assert
        Target.Containers.ShouldBe([first, second], ignoreOrder: true);
    }

    [Fact]
    public async Task EnsureStartedAsyncInitializesARegisteredContainerExactlyOnceAcrossConcurrentCallers()
    {
        // Arrange
#pragma warning disable CA2000 // Ownership transfers to the host under test; its own DisposeAsync is what tears this down.
        var container = new FakeTestContainer();
#pragma warning restore CA2000
        Target.QueueContainer(container);
        await Target.InitializeAsync();

        // Act
        await Task.WhenAll(
            Target.EnsureStartedAsync(container),
            Target.EnsureStartedAsync(container),
            Target.EnsureStartedAsync(container));

        // Assert
        container.InitializeCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureStartedAsyncThrowsForAContainerThatWasNeverRegistered()
    {
        // Arrange
#pragma warning disable CA2000 // Never registered, so the host never takes ownership; nothing to dispose or await.
        var unregistered = new FakeTestContainer();
#pragma warning restore CA2000
        await Target.InitializeAsync();

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => Target.EnsureStartedAsync(unregistered));

        // Assert
        exception.Message.ShouldContain(nameof(FakeTestContainer));
    }

    [Fact]
    public async Task DisposeAsyncOnlyDisposesContainersThatWereActuallyStarted()
    {
        // Arrange
#pragma warning disable CA2000 // Ownership transfers to the host under test; its own DisposeAsync is what tears these down.
        var started = new FakeTestContainer();
        var neverStarted = new FakeTestContainer();
#pragma warning restore CA2000
        Target.QueueContainer(started);
        Target.QueueContainer(neverStarted);
        await Target.InitializeAsync();
        await Target.EnsureStartedAsync(started);

        // Act
        await Target.DisposeAsync();

        // Assert
        started.DisposeCount.ShouldBe(1);
        neverStarted.DisposeCount.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsyncStillDisposesAContainerThatFailedToStart()
    {
        // Arrange
#pragma warning disable CA2000 // Ownership transfers to the host under test; its own DisposeAsync is what tears this down.
        var failing = new FakeTestContainer(() => throw new InvalidOperationException("boom"));
#pragma warning restore CA2000
        Target.QueueContainer(failing);
        await Target.InitializeAsync();

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => Target.EnsureStartedAsync(failing));
        await Target.DisposeAsync();

        // Assert
        failing.DisposeCount.ShouldBe(1);
    }

    public sealed class TestableContainerHost(IMessageSink messageSink) : ContainerHost(messageSink)
    {
        private readonly List<ITestContainer> _pendingContainers = [];

        public int ConfigureContainersCallCount { get; private set; }

        public void QueueContainer(ITestContainer container) => _pendingContainers.Add(container);

        protected override Task ConfigureContainers()
        {
            ConfigureContainersCallCount++;
            foreach (var container in _pendingContainers)
            {
                AddContainer(container);
            }

            return Task.CompletedTask;
        }
    }

    public sealed class FakeTestContainer(Func<ValueTask>? onInitialize = null) : ITestContainer
    {
        public int InitializeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public async ValueTask InitializeAsync()
        {
            InitializeCount++;
            if (onInitialize is not null)
            {
                await onInitialize();
            }
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void ConfigureWebHost(IWebHostBuilder builder)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }
}
