using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class InboxRetentionServiceCollectionExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void NullServicesThrows()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddInboxRetention());
    }

    [Fact]
    public void CanBeCalledWithoutAConfigureDelegate()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddInboxRetention());

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public void RegistersTheSharedTimeProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInboxRetention();
        using var provider = services.BuildServiceProvider();

        // Act
        var timeProvider = provider.GetRequiredService<TimeProvider>();

        // Assert
        timeProvider.ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void RetentionEnabledRegistersTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxRetention(o => o.Retention.Enabled = true);

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(InboxRetentionBackgroundService));
    }

    [Fact]
    public void RetentionDisabledDoesNotRegisterTheRetentionBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInboxRetention(o => o.Retention.Enabled = false);

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(InboxRetentionBackgroundService));
    }

    [Fact]
    public void RetentionEnabledWithAZeroRetentionPeriodFailsValidationAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInboxRetention(o =>
        {
            o.Retention.Enabled = true;
            o.Retention.RetentionPeriod = TimeSpan.Zero;
        });
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<InboxOptions>>().Value);
    }

    [Fact]
    public void RetentionEnabledWithAValidPeriodPassesValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInboxRetention(o =>
        {
            o.Retention.Enabled = true;
            o.Retention.RetentionPeriod = TimeSpan.FromDays(1);
        });
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<InboxOptions>>().Value;

        // Assert
        options.Retention.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void RetentionEnabledWithAZeroBatchSizeFailsValidationAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInboxRetention(o =>
        {
            o.Retention.Enabled = true;
            o.Retention.BatchSize = 0;
        });
        using var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<InboxOptions>>().Value);
    }
}
