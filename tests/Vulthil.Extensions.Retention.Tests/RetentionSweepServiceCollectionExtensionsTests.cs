using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Retention.Tests;

public sealed class RetentionSweepServiceCollectionExtensionsTests : BaseUnitTestCase
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static RetentionSweepSettings DefaultSettings(IServiceProvider _) => new(TimeSpan.FromDays(1), TimeSpan.FromHours(1), 10);

    [Fact]
    public void NullServicesThrows()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddRetentionSweep<IStoreA>("A", DefaultSettings, static _ => null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameThrows(string name)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentException>(() => services.AddRetentionSweep<IStoreA>(name, DefaultSettings, static _ => null));
    }

    [Fact]
    public void NullSettingsThrows()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddRetentionSweep<IStoreA>("A", null!, static _ => null));
    }

    [Fact]
    public void NullDeleterThrows()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddRetentionSweep<IStoreA>("A", DefaultSettings, null!));
    }

    [Fact]
    public void RegistersOneHostedServicePerStoreType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRetentionSweep<IStoreA>("A", DefaultSettings, static _ => null);
        services.AddRetentionSweep<IStoreA>("A again", DefaultSettings, static _ => null);
        services.AddRetentionSweep<IStoreB>("B", DefaultSettings, static _ => null);

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldBe(2);
    }

    [Fact]
    public void RegistersTheSystemClockOnlyWhenNoneIsRegistered()
    {
        // Arrange
        var fresh = new ServiceCollection();
        var preconfigured = new ServiceCollection();
        var fake = new FakeTimeProvider();
        preconfigured.AddSingleton<TimeProvider>(fake);

        // Act
        fresh.AddRetentionSweep<IStoreA>("A", DefaultSettings, static _ => null);
        preconfigured.AddRetentionSweep<IStoreA>("A", DefaultSettings, static _ => null);

        // Assert
        fresh.Single(descriptor => descriptor.ServiceType == typeof(TimeProvider)).ImplementationInstance.ShouldBeSameAs(TimeProvider.System);
        preconfigured.Single(descriptor => descriptor.ServiceType == typeof(TimeProvider)).ImplementationInstance.ShouldBeSameAs(fake);
    }

    [Fact]
    public async Task TheRegisteredSweepDeletesThroughTheStoreWithTheResolvedSettings()
    {
        // Arrange
        var store = new RecordingStoreA();
        var services = new ServiceCollection();
        services.AddRetentionSweep<IStoreA>(
            "A",
            static _ => new RetentionSweepSettings(TimeSpan.FromDays(2), TimeSpan.FromHours(1), 3),
            static resolved => resolved.Delete);
        Use<TimeProvider>(new FakeTimeProvider(Now));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<IStoreA>(store);
        var descriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService));
        var sweep = (IHostedService)descriptor.ImplementationFactory!(AutoMocker);

        // Act
        await sweep.StartAsync(CancellationToken);
        await store.FirstDelete.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await sweep.StopAsync(CancellationToken);

        // Assert
        store.ObservedCutoffs.ShouldHaveSingleItem().ShouldBe(Now - TimeSpan.FromDays(2));
        store.ObservedBatchSizes.ShouldHaveSingleItem().ShouldBe(3);
    }

    public interface IStoreA
    {
        RetentionSweepDeleter? Delete { get; }
    }

    public interface IStoreB
    {
        RetentionSweepDeleter? Delete { get; }
    }

    private sealed class RecordingStoreA : IStoreA
    {
        private readonly TaskCompletionSource _firstDelete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DateTimeOffset> _observedCutoffs = [];
        private readonly List<int> _observedBatchSizes = [];

        public Task FirstDelete => _firstDelete.Task;

        public IReadOnlyList<DateTimeOffset> ObservedCutoffs => _observedCutoffs;

        public IReadOnlyList<int> ObservedBatchSizes => _observedBatchSizes;

        public RetentionSweepDeleter? Delete => DeleteAsync;

        private Task<int> DeleteAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            _observedCutoffs.Add(olderThanUtc);
            _observedBatchSizes.Add(batchSize);
            _firstDelete.TrySetResult();
            return Task.FromResult(0);
        }
    }

    private sealed class AutoMockerServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new PassthroughServiceScope(serviceProvider);

        private sealed class PassthroughServiceScope(IServiceProvider serviceProvider) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = serviceProvider;

            public void Dispose()
            {
            }
        }
    }
}
