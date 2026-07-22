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
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(InboxRetentionBackgroundService));
    }

    [Fact]
    public void RetentionDisabledDoesNotRegisterTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxCore(o => o.Retention.Enabled = false);

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(InboxRetentionBackgroundService));
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
}
