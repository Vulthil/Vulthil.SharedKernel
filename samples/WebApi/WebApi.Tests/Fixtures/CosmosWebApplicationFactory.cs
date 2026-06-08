using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceDefaults;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Boots the WebApi host with a PostgreSQL container (its primary store) and a Cosmos emulator container. The
/// Cosmos container registers <see cref="CosmosProbeDbContext"/> into the host through the container
/// <c>ConfigureServices</c> extension point, so the Cosmos approach is exercised through the real
/// <see cref="BaseWebApplicationFactory{TEntryPoint}"/> pipeline. The broker is swapped for the in-memory harness.
/// </summary>
public sealed class CosmosWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public CosmosWebApplicationFactory(IMessageSink messageSink)
    {
        AddContainer(new PostgreSqlTestContainer(messageSink));
        AddContainer(new CosmosTestContainer<CosmosProbeDbContext>(messageSink));
    }

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting($"ConnectionStrings:{ServiceNames.RabbitMqServiceName}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
    }
}
