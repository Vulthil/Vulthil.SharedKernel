using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
}
