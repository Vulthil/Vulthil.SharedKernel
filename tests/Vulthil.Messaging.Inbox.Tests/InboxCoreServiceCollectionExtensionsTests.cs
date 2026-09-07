using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class InboxCoreServiceCollectionExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void AThirdPartyStoreRegisteredBeforeAddInboxCoreResolvesUnchanged()
    {
        // Arrange — a custom store package registers its own IIdempotencyStore first, exactly as
        // AddRelationalInbox/AddCosmosInbox register theirs before calling AddInboxCore.
        var services = new ServiceCollection();
        services.TryAddScoped<IIdempotencyStore, FakeIdempotencyStore>();

        // Act
        services.AddInboxCore();
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IIdempotencyStore>().ShouldBeOfType<FakeIdempotencyStore>();
    }

    [Fact]
    public void RegistersTheSharedTimeProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxCore();

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void EnableMetricsRegistersTheMeterProviderService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxCore(o => o.EnableMetrics = true);

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void EnableMetricsDisabledSkipsMeterProviderRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxCore(o => o.EnableMetrics = false);

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void RetentionEnabledRegistersTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxCore(o => o.Retention.Enabled = true);

        // Assert
        services.ShouldContain(descriptor => RetentionSweepDescriptors.IsInboxRetentionSweep(descriptor));
    }

    [Fact]
    public void RetentionDisabledDoesNotRegisterTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxCore(o => o.Retention.Enabled = false);

        // Assert
        services.ShouldNotContain(descriptor => RetentionSweepDescriptors.IsInboxRetentionSweep(descriptor));
    }

    [Fact]
    public async Task TheRetentionSweepDeletesThroughTheRegisteredIdempotencyStore()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var retentionPeriod = TimeSpan.FromDays(3);
        var store = new RecordingRetentionStore();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        services.AddScoped<IIdempotencyStore>(_ => store);
        services.AddInboxCore(o =>
        {
            o.EnableMetrics = false;
            o.Retention.Enabled = true;
            o.Retention.RetentionPeriod = retentionPeriod;
        });
        await using var provider = services.BuildServiceProvider();
        var sweep = (IHostedService)services.Single(RetentionSweepDescriptors.IsInboxRetentionSweep).ImplementationFactory!(provider);

        // Act
        await sweep.StartAsync(CancellationToken);
        await store.FirstDelete.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await sweep.StopAsync(CancellationToken);

        // Assert
        store.ObservedCutoffs.ShouldHaveSingleItem().ShouldBe(now - retentionPeriod);
    }

    [Fact]
    public void CanBeCalledWithoutAConfigureDelegate()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddInboxCore());

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public void NullServicesThrows()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddInboxCore());
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore, IInboxRetentionStore
    {
        public Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class RecordingRetentionStore : IIdempotencyStore, IInboxRetentionStore
    {
        private readonly TaskCompletionSource _firstDelete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DateTimeOffset> _observedCutoffs = [];

        public Task FirstDelete => _firstDelete.Task;

        public IReadOnlyList<DateTimeOffset> ObservedCutoffs => _observedCutoffs;

        public Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            _observedCutoffs.Add(olderThanUtc);
            _firstDelete.TrySetResult();
            return Task.FromResult(0);
        }
    }
}
