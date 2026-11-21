using Microsoft.AspNetCore.Hosting;
using ServiceDefaults;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit.Fixtures;

namespace Shipments.Tests.Fixtures;

/// <summary>
/// The same service with the RabbitMQ transport swapped for the in-memory test harness. It consumes only the MySQL
/// container: a publish, send or request then runs its consumers before the call returns, and every message is
/// captured on <see cref="ITestHarness"/>.
/// </summary>
public sealed class HarnessWebApplicationFactory(ShipmentsContainerHost containerHost) : ShipmentsWebApplicationFactory(containerHost)
{
    protected override bool ShouldUseContainer(ITestContainer container) => container is MySqlTestContainer;

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        base.ConfigureCustomWebHost(builder);

        // No broker is consumed, so the RabbitMQ client gets a placeholder connection string; the swapped transport
        // never opens it.
        builder.UseSetting($"ConnectionStrings:{ServiceNames.RabbitMqServiceName}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
    }
}
