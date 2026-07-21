using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Relational.Tests;

public sealed class RelationalInboxExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void RegistersTheRelationalIdempotencyStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelationalInbox<TestDbContext>();

        // Assert
        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IIdempotencyStore)
            && descriptor.ImplementationType == typeof(RelationalIdempotencyStore<TestDbContext>)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegistersTheSharedTimeProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelationalInbox<TestDbContext>();

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void EnableMetricsRegistersTheMeterProviderService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelationalInbox<TestDbContext>(o => o.EnableMetrics = true);

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void EnableMetricsDisabledSkipsMeterProviderRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelationalInbox<TestDbContext>(o => o.EnableMetrics = false);

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void RetentionEnabledRegistersAHostedService()
    {
        // Arrange — InboxRetentionBackgroundService is internal to Vulthil.Messaging.Inbox and not visible here, so
        // metrics (the only other IHostedService source reachable from AddRelationalInbox) are turned off to isolate
        // the retention gate.
        var services = new ServiceCollection();

        // Act
        services.AddRelationalInbox<TestDbContext>(o =>
        {
            o.EnableMetrics = false;
            o.Retention.Enabled = true;
        });

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void RetentionDisabledDoesNotRegisterAHostedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelationalInbox<TestDbContext>(o =>
        {
            o.EnableMetrics = false;
            o.Retention.Enabled = false;
        });

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), ISaveInboxMessages
    {
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    }
}
