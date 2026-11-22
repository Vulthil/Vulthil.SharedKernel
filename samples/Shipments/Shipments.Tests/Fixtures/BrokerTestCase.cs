using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Shipments.Tests.Fixtures;

public abstract class BrokerTestCase(ShipmentsWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<ShipmentsWebApplicationFactory>
{
    protected IPublisher Publisher => ScopedServices.GetRequiredService<IPublisher>();

    /// <summary>
    /// A publish to a topic exchange is dropped while no queue is bound to it, so a test that publishes straight to
    /// the broker first waits until the transport has declared its topology.
    /// </summary>
    protected Task WaitForBrokerAsync() =>
        Factory.Services.GetRequiredService<ITransport>().WaitUntilReadyAsync(CancellationToken);
}
