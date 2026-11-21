using Microsoft.AspNetCore.Hosting;
using ServiceDefaults;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Boots the same production composition with the RabbitMQ transport swapped for the in-memory test harness. It
/// consumes only the PostgreSQL container: the WebApi sends to a queue of the Shipments service, which no broker in
/// this test run hosts, so the harness captures the send instead.
/// </summary>
public sealed class HarnessWebApplicationFactory(AppContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override bool ShouldUseContainer(ITestContainer container) => container is PostgreSqlTestContainer;

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        // No broker is consumed, so the RabbitMQ client gets a placeholder connection string; the swapped transport
        // never opens it.
        builder.UseSetting($"ConnectionStrings:{ServiceNames.RabbitMqServiceName}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
    }
}
