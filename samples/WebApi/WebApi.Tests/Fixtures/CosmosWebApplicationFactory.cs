using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceDefaults;
using Vulthil.Messaging.Inbox;
using Vulthil.Messaging.Inbox.Cosmos;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Boots the WebApi host against the shared <see cref="AppContainerHost"/> containers: a PostgreSQL database (its
/// primary store) and a Cosmos emulator database, both scoped per test class so classes run in parallel. The Cosmos
/// container registers <see cref="CosmosProbeDbContext"/> into the host through the container
/// <c>ConfigureServices</c> extension point, so the Cosmos approach is exercised through the real
/// <see cref="BaseWebApplicationFactory{TEntryPoint}"/> pipeline. The broker is swapped for the in-memory harness.
/// </summary>
public sealed class CosmosWebApplicationFactory(AppContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override bool ShouldUseContainer(ITestContainer container) => container is not RabbitMqTestContainer;

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting($"ConnectionStrings:{ServiceNames.RabbitMqServiceName}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services =>
        {
            services.ReplaceTransportWithTestHarness();

            // The WebApi host registers a relational idempotency store; this factory exists to exercise the Cosmos
            // approach, so replace it with the Cosmos-backed store pointed at the emulator.
            services.RemoveAll<IIdempotencyStore>();
            services.AddCosmosInbox<CosmosProbeDbContext>();
        });
    }
}
