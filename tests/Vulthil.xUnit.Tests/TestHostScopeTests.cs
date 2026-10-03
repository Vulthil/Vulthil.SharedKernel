using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Extensions.Hosting;
using Vulthil.Extensions.Testing;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit.Tests;

public sealed class TestHostScopeTests : BaseUnitTestCase
{
    private const string ScopeId = "scope_1";

    private readonly ConcurrentQueue<string> _log = new();
    private readonly List<ITestContainer> _hostContainers = [];
    private readonly HashSet<ITestContainer> _excludedContainers = [];
    private readonly Lazy<TestHostScope> _lazyTarget;

    private TestHostScope Target => _lazyTarget.Value;

    public TestHostScopeTests() =>
        _lazyTarget = new(() => new TestHostScope(
            new FakeContainerHost(_hostContainers),
            container => !_excludedContainers.Contains(container),
            () => ScopeId,
            new FakeTimeProvider()));

    [Fact]
    public async Task InitializeAsyncStartsOnlyTheConsumedContainersAndScopesThemUnderOneId()
    {
        // Arrange
        var first = AddContainer("first", "FirstDb");
        var excluded = AddContainer("excluded", "ExcludedDb");
        var second = AddContainer("second", "SecondDb");
        _excludedContainers.Add(excluded);

        // Act
        await Target.InitializeAsync();

        // Assert
        first.StartCount.ShouldBe(1);
        second.StartCount.ShouldBe(1);
        excluded.StartCount.ShouldBe(0);
        first.ScopeIds.ShouldBe([ScopeId]);
        second.ScopeIds.ShouldBe([ScopeId]);
        Target.Containers.Count.ShouldBe(2);
        _log.ShouldContain("init:first");
        _log.ShouldContain("init:second");
    }

    [Fact]
    public async Task InitializeAsyncRunsOnce()
    {
        // Arrange
        var container = AddContainer("db", "Db");

        // Act
        await Target.InitializeAsync();
        await Target.InitializeAsync();

        // Assert
        container.StartCount.ShouldBe(1);
        container.ScopeIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConnectionStringsHoldOneEntryPerConsumedContainer()
    {
        // Arrange
        AddContainer("orders", "Orders");
        AddContainer("broker", "Broker");

        // Act
        await Target.InitializeAsync();

        // Assert
        Target.ConnectionStrings.ToDictionary().ShouldBe(new Dictionary<string, string>
        {
            ["ConnectionStrings:Orders"] = "orders-connection",
            ["ConnectionStrings:Broker"] = "broker-connection",
        });
    }

    [Fact]
    public async Task TwoConsumedContainersWithTheSameKeyFailBeforeTheScopeIsProvisioned()
    {
        // Arrange
        AddContainer("first", "AppDb");
        AddContainer("second", "appdb");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(Target.InitializeAsync);

        // Assert
        exception.Message.ShouldContain("'AppDb'");
        exception.Message.ShouldContain(nameof(FakeContainer));
        exception.Message.ShouldContain("ShouldUseContainer");
        _log.ShouldNotContain(entry => entry.StartsWith("init:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AContainerThatSharesAKeyButIsNotConsumedIsAllowed()
    {
        // Arrange
        AddContainer("consumed", "AppDb");
        _excludedContainers.Add(AddContainer("excluded", "AppDb"));

        // Act & Assert
        await Should.NotThrowAsync(Target.InitializeAsync);
    }

    [Fact]
    public async Task AStartingHostRunsTheMigrationsBeforeTheStartupResources()
    {
        // Arrange
        AddContainer("db", "Db");
        await Target.InitializeAsync();
        await using var host = HostWith();

        // Act
        await Target.CreateHostLifecycle(host).StartAsync(CancellationToken);

        // Assert
        _log.Where(entry => entry.StartsWith("migrate:", StringComparison.Ordinal) || entry.StartsWith("startup:", StringComparison.Ordinal))
            .ShouldBe(["migrate:db", "startup:db"]);
    }

    [Fact]
    public async Task ASecondHostPausesTheFirstHostsRestartableServicesUntilItStops()
    {
        // Arrange
        await Target.InitializeAsync();
        await using var sharedHost = HostWith(new RecordingService(_log, "shared"));
        await using var testHost = HostWith(new RecordingService(_log, "test"));
        await Target.CreateHostLifecycle(sharedHost).StartAsync(CancellationToken);
        var testHostLifecycle = Target.CreateHostLifecycle(testHost);

        // Act
        await testHostLifecycle.StartAsync(CancellationToken);
        var whileTheTestHostRuns = ServiceEvents();
        await testHostLifecycle.StopAsync(CancellationToken);

        // Assert
        whileTheTestHostRuns.ShouldBe(["stop:shared"]);
        ServiceEvents().ShouldBe(["stop:shared", "start:shared"]);
    }

    [Fact]
    public async Task AnOlderHostThatStopsFirstIsNotResumedWhenTheNewerHostStops()
    {
        // Arrange
        await Target.InitializeAsync();
        await using var sharedHost = HostWith(new RecordingService(_log, "shared"));
        await using var testHost = HostWith(new RecordingService(_log, "test"));
        var sharedHostLifecycle = Target.CreateHostLifecycle(sharedHost);
        var testHostLifecycle = Target.CreateHostLifecycle(testHost);
        await sharedHostLifecycle.StartAsync(CancellationToken);
        await testHostLifecycle.StartAsync(CancellationToken);

        // Act
        await sharedHostLifecycle.StopAsync(CancellationToken);
        await testHostLifecycle.StopAsync(CancellationToken);

        // Assert
        ServiceEvents().ShouldBe(["stop:shared"]);
    }

    [Fact]
    public async Task ResetPausesTheRunningHostAndResetsTheResourcesWithTheNewestHost()
    {
        // Arrange
        var container = AddContainer("db", "Db");
        await Target.InitializeAsync();
        await using var sharedHost = HostWith(new RecordingService(_log, "shared"));
        await using var testHost = HostWith(new RecordingService(_log, "test"));
        await Target.CreateHostLifecycle(sharedHost).StartAsync(CancellationToken);
        await Target.CreateHostLifecycle(testHost).StartAsync(CancellationToken);

        // Act
        await Target.ResetAsync([new RecordingResource(_log, "http")]);

        // Assert
        ServiceEvents().ShouldBe(["stop:shared", "stop:test", "start:test"]);
        _log.ShouldContain("reset:db");
        _log.ShouldContain("reset:http");
        container.View!.ResetServices.ShouldHaveSingleItem().ShouldBeSameAs(testHost);
    }

    [Fact]
    public async Task ResetResetsTheTestStatesOfEveryLiveHost()
    {
        // Arrange
        await Target.InitializeAsync();
        await using var sharedHost = HostWithTestStates(new RecordingTestState(_log, "shared"));
        await using var testHost = HostWithTestStates(new RecordingTestState(_log, "test"));
        await Target.CreateHostLifecycle(sharedHost).StartAsync(CancellationToken);
        await Target.CreateHostLifecycle(testHost).StartAsync(CancellationToken);

        // Act
        await Target.ResetAsync([]);

        // Assert
        TestStateResets().ShouldBe(["reset-state:shared", "reset-state:test"], ignoreOrder: true);
    }

    [Fact]
    public async Task ATestStateThatTwoLiveHostsShareIsResetOnce()
    {
        // Arrange
        var sharedState = new RecordingTestState(_log, "shared");
        await Target.InitializeAsync();
        await using var sharedHost = HostWithTestStates(sharedState);
        await using var testHost = HostWithTestStates(sharedState);
        await Target.CreateHostLifecycle(sharedHost).StartAsync(CancellationToken);
        await Target.CreateHostLifecycle(testHost).StartAsync(CancellationToken);

        // Act
        await Target.ResetAsync([]);

        // Assert
        TestStateResets().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TheTestStatesOfAHostThatStoppedAreNotReset()
    {
        // Arrange
        await Target.InitializeAsync();
        await using var sharedHost = HostWithTestStates(new RecordingTestState(_log, "shared"));
        await using var testHost = HostWithTestStates(new RecordingTestState(_log, "test"));
        var testHostLifecycle = Target.CreateHostLifecycle(testHost);
        await Target.CreateHostLifecycle(sharedHost).StartAsync(CancellationToken);
        await testHostLifecycle.StartAsync(CancellationToken);
        await testHostLifecycle.StopAsync(CancellationToken);

        // Act
        await Target.ResetAsync([]);

        // Assert
        TestStateResets().ShouldBe(["reset-state:shared"]);
    }

    [Fact]
    public async Task ResetWithoutALiveHostDoesNothing()
    {
        // Arrange
        AddContainer("db", "Db");
        await Target.InitializeAsync();

        // Act
        await Target.ResetAsync([new RecordingResource(_log, "http")]);

        // Assert
        _log.ShouldNotContain(entry => entry.StartsWith("reset:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AHostWhosePauseFailedStillJoinsTheLiveHosts()
    {
        // Arrange
        await Target.InitializeAsync();
        await using var sharedHost = HostWith(new RecordingService(_log, "shared", onStop: () => throw new InvalidOperationException("stuck")));
        await using var testHost = HostWith(new RecordingService(_log, "test"));
        await Target.CreateHostLifecycle(sharedHost).StartAsync(CancellationToken);

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => Target.CreateHostLifecycle(testHost).StartAsync(CancellationToken));
        await Target.ResetAsync([]);

        // Assert
        exception.Message.ShouldContain("Pausing");
        ServiceEvents().ShouldBe(["stop:shared", "stop:test", "start:test"]);
    }

    [Fact]
    public async Task DisposeAsyncDisposesTheScopeViews()
    {
        // Arrange
        AddContainer("db", "Db");
        await Target.InitializeAsync();

        // Act
        await Target.DisposeAsync();

        // Assert
        _log.ShouldContain("dispose:db");
    }

    private FakeContainer AddContainer(string name, string connectionStringKey)
    {
        var container = new FakeContainer(_log, name, connectionStringKey);
        _hostContainers.Add(container);
        return container;
    }

    private string[] ServiceEvents() =>
        [.. _log.Where(entry => entry.StartsWith("start:", StringComparison.Ordinal) || entry.StartsWith("stop:", StringComparison.Ordinal))];

    private string[] TestStateResets() =>
        [.. _log.Where(entry => entry.StartsWith("reset-state:", StringComparison.Ordinal))];

    private static ServiceProvider HostWith(params IHostedService[] services)
    {
        var collection = new ServiceCollection();
        foreach (var service in services)
        {
            collection.AddSingleton(service);
        }

        return collection.BuildServiceProvider();
    }

    private static ServiceProvider HostWithTestStates(params IResettableTestState[] testStates)
    {
        var collection = new ServiceCollection();
        foreach (var testState in testStates)
        {
            collection.AddSingleton(testState);
        }

        return collection.BuildServiceProvider();
    }

    public sealed class FakeContainerHost : ContainerHost
    {
        public FakeContainerHost(IEnumerable<ITestContainer> containers)
        {
            foreach (var container in containers)
            {
                AddContainer(container);
            }
        }
    }

    /// <summary>
    /// A shared container that counts its starts and mints a <see cref="FakeView"/> per scope.
    /// </summary>
    public sealed class FakeContainer(ConcurrentQueue<string> log, string name, string connectionStringKey)
        : ITestContainerWithConnectionString, ITestContainerScopeProvider
    {
        private readonly List<string> _scopeIds = [];
        private int _startCount;

        public int StartCount => Volatile.Read(ref _startCount);

        public IReadOnlyList<string> ScopeIds => _scopeIds;

        public FakeView? View { get; private set; }

        public string ConnectionString => $"{name}-host-connection";

        public string ConnectionStringKey => connectionStringKey;

        public ITestContainer CreateScope(string scopeId)
        {
            _scopeIds.Add(scopeId);
            View = new FakeView(log, name, connectionStringKey);
            return View;
        }

        public ValueTask InitializeAsync()
        {
            Interlocked.Increment(ref _startCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void ConfigureWebHost(IWebHostBuilder builder)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }

    /// <summary>
    /// A scope view that records its lifecycle, its migration, its startup setup and its resets.
    /// </summary>
    public sealed class FakeView(ConcurrentQueue<string> log, string name, string connectionStringKey)
        : ITestDatabaseContainer, IStartupResource
    {
        public ConcurrentQueue<IServiceProvider> ResetServices { get; } = new();

        public string ConnectionString => $"{name}-connection";

        public string ConnectionStringKey => connectionStringKey;

        public ValueTask InitializeAsync()
        {
            log.Enqueue($"init:{name}");
            return ValueTask.CompletedTask;
        }

        public ValueTask InitializeAsync(IServiceProvider serviceProvider)
        {
            log.Enqueue($"startup:{name}");
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            log.Enqueue($"dispose:{name}");
            return ValueTask.CompletedTask;
        }

        public ValueTask MigrateDatabase(IServiceProvider serviceProvider)
        {
            log.Enqueue($"migrate:{name}");
            return ValueTask.CompletedTask;
        }

        public ValueTask ResetAsync(IServiceProvider serviceProvider)
        {
            ResetServices.Enqueue(serviceProvider);
            log.Enqueue($"reset:{name}");
            return ValueTask.CompletedTask;
        }

        public void ConfigureWebHost(IWebHostBuilder builder)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }

    public sealed class RecordingService(ConcurrentQueue<string> log, string name, Action? onStop = null) : IRestartableHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            log.Enqueue($"start:{name}");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            log.Enqueue($"stop:{name}");
            onStop?.Invoke();
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingResource(ConcurrentQueue<string> log, string name) : IResettableResource
    {
        public ValueTask ResetAsync(IServiceProvider serviceProvider)
        {
            log.Enqueue($"reset:{name}");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class RecordingTestState(ConcurrentQueue<string> log, string name) : IResettableTestState
    {
        public ValueTask ResetAsync(CancellationToken cancellationToken = default)
        {
            log.Enqueue($"reset-state:{name}");
            return ValueTask.CompletedTask;
        }
    }
}
