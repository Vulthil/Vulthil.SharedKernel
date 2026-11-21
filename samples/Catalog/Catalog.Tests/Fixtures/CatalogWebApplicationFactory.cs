using Microsoft.AspNetCore.Hosting;
using ServiceDefaults;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit;

namespace Catalog.Tests.Fixtures;

public sealed class CatalogWebApplicationFactory(CatalogContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        // No broker is consumed, so the RabbitMQ client gets a placeholder connection string; the swapped transport
        // never opens it.
        builder.UseSetting($"ConnectionStrings:{ServiceNames.RabbitMqServiceName}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
    }
}
