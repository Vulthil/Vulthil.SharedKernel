using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit.Tests.Fixtures;

public sealed class TestContainerScopeTests : BaseUnitTestCase
{
    private const string NamespaceName = "orders_1";

    private readonly SharedContainer _container = new();

    [Fact]
    public async Task AScopeCreatesItsNamespaceWhenItInitializesAndDeletesItWhenItIsDisposed()
    {
        // Arrange
        await using var scope = new NamespacedScope(_container, NamespaceName);

        // Act
        await scope.InitializeAsync();
        await scope.DisposeAsync();

        // Assert
        scope.Calls.ShouldBe([$"create:{NamespaceName}", $"delete:{NamespaceName}"]);
        _container.InitializeCount.ShouldBe(0);
        _container.DisposeCount.ShouldBe(0);
    }

    [Fact]
    public async Task AScopeWithoutANamespaceNeitherCreatesNorDeletesOne()
    {
        // Arrange
        await using var scope = new NamespacedScope(_container, namespaceName: null);

        // Act
        await scope.InitializeAsync();
        await scope.DisposeAsync();

        // Assert
        scope.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFailedDeleteOfAnyKindIsReportedInsteadOfThrown()
    {
        // Arrange
        await using var scope = new NamespacedScope(_container, NamespaceName)
        {
            DeleteFailure = new InvalidOperationException("The broker is gone."),
        };

        // Act
        await Should.NotThrowAsync(() => scope.DisposeAsync().AsTask());

        // Assert
        scope.Calls.ShouldBe([$"delete:{NamespaceName}"]);
    }

    [Fact]
    public async Task AScopeForwardsTheHostConfigurationAndTheConnectionStringKeyToTheSharedContainer()
    {
        // Arrange
        await using var scope = new NamespacedScope(_container, NamespaceName);
        var builder = Mock.Of<IWebHostBuilder>();
        var services = new ServiceCollection();

        // Act
        scope.ConfigureWebHost(builder);
        scope.ConfigureServices(services);

        // Assert
        _container.ConfiguredWebHost.ShouldBeSameAs(builder);
        _container.ConfiguredServices.ShouldBeSameAs(services);
        scope.ConnectionStringKey.ShouldBe(_container.ConnectionStringKey);
    }

    [Fact]
    public async Task AScopeForwardsTheConnectionStringUnlessItsNamespaceHasAnAddressOfItsOwn()
    {
        // Arrange
        await using var forwarding = new NamespacedScope(_container, NamespaceName);
        await using var addressed = new NamespacedScope(_container, NamespaceName) { ConnectionStringOverride = "Host=shared;Database=orders_1" };

        // Act & Assert
        forwarding.ConnectionString.ShouldBe(_container.ConnectionString);
        addressed.ConnectionString.ShouldBe("Host=shared;Database=orders_1");
    }

    [Fact]
    public void TheBaseViewNeverResetsTheSharedContainer()
    {
        // Act
        var interfaces = typeof(TestContainerScope<>).GetInterfaces();

        // Assert
        interfaces.ShouldNotContain(typeof(IResettableResource));
    }

    public sealed class SharedContainer : ITestContainerWithConnectionString
    {
        public int InitializeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public IWebHostBuilder? ConfiguredWebHost { get; private set; }

        public IServiceCollection? ConfiguredServices { get; private set; }

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
    }

    public sealed class NamespacedScope(SharedContainer container, string? namespaceName)
        : TestContainerWithConnectionStringScope<SharedContainer>(container, namespaceName)
    {
        private readonly List<string> _calls = [];

        public IReadOnlyList<string> Calls => _calls;

        public Exception? DeleteFailure { get; init; }

        public string? ConnectionStringOverride { get; init; }

        public override string ConnectionString => ConnectionStringOverride ?? base.ConnectionString;

        protected override ValueTask CreateNamespaceAsync(string namespaceName)
        {
            _calls.Add($"create:{namespaceName}");
            return ValueTask.CompletedTask;
        }

        protected override ValueTask DeleteNamespaceAsync(string namespaceName)
        {
            _calls.Add($"delete:{namespaceName}");
            return DeleteFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(DeleteFailure);
        }
    }
}
