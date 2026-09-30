using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Infrastructure.Relational.Tests;

public sealed class RelationalOutboxServiceCollectionExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void TheCommitTriggerRegistersOneCommitInterceptorAndOneSingletonRelayWakeupWhenCalledTwice()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(GetMock<IOutboxSignal>().Object);

        // Act
        services.AddRelationalOutboxCommitTrigger();
        services.AddRelationalOutboxCommitTrigger();

        // Assert
        services.Where(descriptor => descriptor.ServiceType == typeof(OutboxRelayWakeup))
            .ShouldHaveSingleItem().Lifetime.ShouldBe(ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IOutboxInterceptor>().ShouldHaveSingleItem().ShouldBeOfType<OutboxCommitInterceptor>();
    }
}
