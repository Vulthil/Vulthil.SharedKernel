using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit.Tests.Fixtures;

public sealed class SharedContainerScopeTests : BaseUnitTestCase
{
    private readonly RecordingContainer _container = new();

    public sealed class RecordingContainer : ITestContainerWithConnectionString, IResettableResource
    {
        public int InitializeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public IWebHostBuilder? ConfiguredWebHost { get; private set; }

        public IServiceCollection? ConfiguredServices { get; private set; }

        public IServiceProvider? ResetWith { get; private set; }

        public string ConnectionString => "Host=shared;Database=shared";

        public string ConnectionStringKey => "SharedDb";

        public ValueTask InitializeAsync()
        {
            InitializeCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void ConfigureWebHost(IWebHostBuilder builder) => ConfiguredWebHost = builder;

        public void ConfigureServices(IServiceCollection services) => ConfiguredServices = services;

        public ValueTask ResetAsync(IServiceProvider serviceProvider)
        {
            ResetWith = serviceProvider;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class PlainContainer : ITestContainer
    {
        public ValueTask InitializeAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void ConfigureWebHost(IWebHostBuilder builder)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }

    public sealed class StartupContainer : ITestContainer, IStartupResource
    {
        public IServiceProvider? StartedWith { get; private set; }

        public ValueTask InitializeAsync() => ValueTask.CompletedTask;

        public ValueTask InitializeAsync(IServiceProvider serviceProvider)
        {
            StartedWith = serviceProvider;
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

    public sealed class DatabaseContainer : ITestDatabaseContainer, IStartupResource
    {
        public IServiceProvider? MigratedWith { get; private set; }

        public IServiceProvider? StartedWith { get; private set; }

        public string ConnectionString => "Host=shared;Database=app";

        public string ConnectionStringKey => "AppDb";

        public ValueTask InitializeAsync() => ValueTask.CompletedTask;

        public ValueTask InitializeAsync(IServiceProvider serviceProvider)
        {
            StartedWith = serviceProvider;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask MigrateDatabase(IServiceProvider serviceProvider)
        {
            MigratedWith = serviceProvider;
            return ValueTask.CompletedTask;
        }

        public ValueTask ResetAsync(IServiceProvider serviceProvider) => ValueTask.CompletedTask;

        public void ConfigureWebHost(IWebHostBuilder builder)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }

    [Fact]
    public async Task TheScopeLifecycleNeverTouchesTheSharedContainer()
    {
        // Arrange
        await using var scope = new SharedContainerScope(_container);

        // Act
        await scope.InitializeAsync();
        await scope.DisposeAsync();

        // Assert
        _container.InitializeCount.ShouldBe(0);
        _container.DisposeCount.ShouldBe(0);
    }

    [Fact]
    public async Task HostConfigurationIsForwardedToTheSharedContainer()
    {
        // Arrange
        await using var scope = new SharedContainerScope(_container);
        var builder = Mock.Of<IWebHostBuilder>();
        var services = new ServiceCollection();

        // Act
        scope.ConfigureWebHost(builder);
        scope.ConfigureServices(services);

        // Assert
        _container.ConfiguredWebHost.ShouldBeSameAs(builder);
        _container.ConfiguredServices.ShouldBeSameAs(services);
    }

    [Fact]
    public async Task ResetIsForwardedToAResettableContainerWithTheSameServiceProvider()
    {
        // Arrange
        await using var scope = new SharedContainerScope(_container);
        var serviceProvider = Mock.Of<IServiceProvider>();

        // Act
        await scope.ResetAsync(serviceProvider);

        // Assert
        _container.ResetWith.ShouldBeSameAs(serviceProvider);
    }

    [Fact]
    public async Task ResetIsANoOpForAContainerThatIsNotResettable()
    {
        // Arrange
        await using var plain = new PlainContainer();
        await using var scope = new SharedContainerScope(plain);

        // Act & Assert
        await Should.NotThrowAsync(() => scope.ResetAsync(Mock.Of<IServiceProvider>()).AsTask());
    }

    [Fact]
    public async Task TheConnectionStringScopeForwardsTheConnectionStringAndItsKeyUnchanged()
    {
        // Arrange
        await using var scope = new SharedContainerWithConnectionStringScope(_container);

        // Act & Assert
        scope.ConnectionString.ShouldBe(_container.ConnectionString);
        scope.ConnectionStringKey.ShouldBe(_container.ConnectionStringKey);
    }

    [Fact]
    public async Task StartupSetupIsForwardedToAStartupContainerWithTheSameServiceProvider()
    {
        // Arrange
        await using var startup = new StartupContainer();
        await using var scope = new SharedContainerScope(startup);
        var serviceProvider = Mock.Of<IServiceProvider>();

        // Act
        await scope.InitializeAsync(serviceProvider);

        // Assert
        startup.StartedWith.ShouldBeSameAs(serviceProvider);
    }

    [Fact]
    public async Task StartupSetupIsANoOpForAContainerWithoutIt()
    {
        // Arrange
        await using var plain = new PlainContainer();
        await using var scope = new SharedContainerScope(plain);

        // Act & Assert
        await Should.NotThrowAsync(() => scope.InitializeAsync(Mock.Of<IServiceProvider>()).AsTask());
    }

    [Fact]
    public async Task TheConnectionStringScopeForwardsTheMigrationAndTheStartupSetupWithTheSameServiceProviders()
    {
        // Arrange
        await using var database = new DatabaseContainer();
        await using var scope = new SharedContainerWithConnectionStringScope(database);
        var scopedServices = Mock.Of<IServiceProvider>();
        var rootServices = Mock.Of<IServiceProvider>();

        // Act
        await scope.MigrateDatabase(scopedServices);
        await scope.InitializeAsync(rootServices);

        // Assert
        database.MigratedWith.ShouldBeSameAs(scopedServices);
        database.StartedWith.ShouldBeSameAs(rootServices);
    }

    [Fact]
    public async Task TheConnectionStringScopeMigrationAndStartupSetupAreNoOpsForAContainerWithoutThem()
    {
        // Arrange
        await using var scope = new SharedContainerWithConnectionStringScope(_container);

        // Act & Assert
        await Should.NotThrowAsync(async () =>
        {
            await scope.MigrateDatabase(Mock.Of<IServiceProvider>());
            await scope.InitializeAsync(Mock.Of<IServiceProvider>());
        });
    }
}
