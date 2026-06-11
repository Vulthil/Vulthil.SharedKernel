using Microsoft.AspNetCore.Hosting;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Boots the test host with its full messaging composition, then swaps the broker transport for the in-memory test
/// harness — running the real consumers with a real database but no message broker. Only the shared PostgreSQL
/// container is consumed (with a database per test class).
/// </summary>
public sealed class TestHarnessWebApplicationFactory(IntegrationTestContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override bool ShouldUseContainer(ITestContainer container) => container is PostgreSqlTestContainer;

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        // No broker container is consumed, so stub the connection string to satisfy the RabbitMQ client registration;
        // swapping the transport means the broker connection is never resolved.
        builder.UseSetting($"ConnectionStrings:{TestHostConnectionStrings.RabbitMq}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
    }
}
