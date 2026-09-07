using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

public sealed class OutboxEngineServiceCollectionExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void MaxDelaySecondsLessThanOutboxProcessingDelaySecondsFailsValidationAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o =>
        {
            o.OutboxProcessingDelaySeconds = 100;
            o.MaxDelaySeconds = 1;
        });
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Fact]
    public void MaxDelaySecondsEqualToOutboxProcessingDelaySecondsPassesValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o =>
        {
            o.OutboxProcessingDelaySeconds = 5;
            o.MaxDelaySeconds = 5;
        });
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value;

        // Assert
        options.MaxDelaySeconds.ShouldBe(5);
    }

    [Fact]
    public void SingleOutboxStoreRegistrationIsUnaffected()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine();

        // Act
        var exception = Record.Exception(() => services.AddScoped<IOutboxStore, FakeOutboxStore<OutboxProcessor>>());

        // Assert
        exception.ShouldBeNull();
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IOutboxStore) && descriptor.ImplementationType == typeof(FakeOutboxStore<OutboxProcessor>));
    }

    [Fact]
    public void SecondOutboxStoreRegistrationThrowsDescriptiveError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine();
        services.AddScoped<IOutboxStore, FakeOutboxStore<OutboxProcessor>>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => services.AddOutboxEngine());

        // Assert
        exception.Message.ShouldContain(nameof(OutboxProcessor));
        exception.Message.ShouldContain("one outbox-enabled DbContext");
        exception.Message.ShouldContain("IOutboxStore");
    }

    [Fact]
    public void CallingAddOutboxEngineTwiceDoesNotDuplicateHostedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine();

        // Act
        services.AddOutboxEngine();

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(OutboxProcessor)).ShouldBe(1);
    }

    [Fact]
    public void EnableMetricsRegistersTheMeterProviderService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxEngine(o => o.EnableMetrics = true);

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void EnableMetricsDisabledSkipsMeterProviderRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxEngine(o => o.EnableMetrics = false);

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void EnableTracingRegistersTheTracerProviderService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxEngine(o => o.EnableTracing = true);

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(TracerProvider));
    }

    [Fact]
    public void EnableTracingDisabledSkipsTracerProviderRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxEngine(o => o.EnableTracing = false);

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(TracerProvider));
    }

    [Fact]
    public void RetentionEnabledRegistersTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxEngine(o => o.Retention.Enabled = true);

        // Assert
        services.ShouldContain(descriptor => IsOutboxRetentionSweep(descriptor));
    }

    [Fact]
    public void RetentionDisabledDoesNotRegisterTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOutboxEngine(o => o.Retention.Enabled = false);

        // Assert
        services.ShouldNotContain(descriptor => IsOutboxRetentionSweep(descriptor));
    }

    [Fact]
    public async Task TheRetentionSweepDeletesThroughTheRegisteredOutboxStore()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var retentionPeriod = TimeSpan.FromDays(3);
        var store = new RecordingRetentionStore();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        services.AddOutboxEngine(o =>
        {
            o.EnableTracing = false;
            o.EnableMetrics = false;
            o.Retention.Enabled = true;
            o.Retention.RetentionPeriod = retentionPeriod;
        });
        services.AddScoped<IOutboxStore>(_ => store);
        await using var provider = services.BuildServiceProvider();
        var sweep = (IHostedService)services.Single(IsOutboxRetentionSweep).ImplementationFactory!(provider);

        // Act
        await sweep.StartAsync(CancellationToken);
        await store.FirstDelete.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await sweep.StopAsync(CancellationToken);

        // Assert
        store.ObservedCutoffs.ShouldHaveSingleItem().ShouldBe(now - retentionPeriod);
    }

    /// <summary>
    /// The sweep's hosted service type lives in Vulthil.Extensions.Retention and is internal there, so it is recognised
    /// by the shape of its factory registration: a hosted service built for the outbox store type.
    /// </summary>
    private static bool IsOutboxRetentionSweep(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationFactory?.GetType().GenericTypeArguments is [_, { IsGenericType: true } sweepType]
        && sweepType.GetGenericArguments()[0] == typeof(IOutboxStore);

    private sealed class RecordingRetentionStore : IOutboxStore, IOutboxRetentionStore
    {
        private readonly TaskCompletionSource _firstDelete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DateTimeOffset> _observedCutoffs = [];

        public Task FirstDelete => _firstDelete.Task;

        public IReadOnlyList<DateTimeOffset> ObservedCutoffs => _observedCutoffs;

        public bool IsInTransaction => false;

        public void AddOutboxMessage(OutboxMessage message)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            _observedCutoffs.Add(olderThanUtc);
            _firstDelete.TrySetResult();
            return Task.FromResult(0);
        }
    }

    [Fact]
    public void RetentionEnabledWithAZeroRetentionPeriodFailsValidationAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o =>
        {
            o.Retention.Enabled = true;
            o.Retention.RetentionPeriod = TimeSpan.Zero;
        });
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void OutboxProcessingDelaySecondsOutsideOneToOneHundredFailsValidationAtStartup(int value)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o => o.OutboxProcessingDelaySeconds = value);
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void MaxDelaySecondsOutsideOneToThreeHundredFailsValidationAtStartup(int value)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o => o.MaxDelaySeconds = value);
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Fact]
    public void ZeroBatchSizeFailsValidationAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o => o.BatchSize = 0);
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Fact]
    public void ZeroMaxRetriesFailsValidationAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o => o.MaxRetries = 0);
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void MaxDegreeOfParallelismOutsideOneToOneHundredFailsValidationAtStartup(int value)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine(o => o.MaxDegreeOfParallelism = value);
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value);
    }

    [Fact]
    public void DefaultOptionsPassValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOutboxEngine();
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value;

        // Assert
        options.ShouldNotBeNull();
    }

    private sealed class FakeOutboxStore<TContext> : IOutboxStore
    {
        public Type ContextType { get; } = typeof(TContext);

        public bool IsInTransaction => false;

        public void AddOutboxMessage(OutboxMessage message)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
