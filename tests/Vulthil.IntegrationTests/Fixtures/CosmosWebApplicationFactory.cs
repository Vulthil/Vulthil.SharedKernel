using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Inbox;
using Vulthil.Messaging.Inbox.Cosmos;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Boots the test host against the shared PostgreSQL server (its primary store) and the shared Cosmos emulator, both
/// scoped per test class. The Cosmos container registers <see cref="CosmosProbeDbContext"/> into the host through the
/// container <c>ConfigureServices</c> extension point, so the Cosmos approach is exercised through the real
/// <see cref="BaseWebApplicationFactory{TEntryPoint}"/> pipeline. The broker is swapped for the in-memory harness.
/// </summary>
public sealed class CosmosWebApplicationFactory(IntegrationTestContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting($"ConnectionStrings:{TestHostConnectionStrings.RabbitMq}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services =>
        {
            services.ReplaceTransportWithTestHarness();

            // The test host registers a relational idempotency store; this factory exists to exercise the Cosmos
            // approach, so replace it with the Cosmos-backed store pointed at the emulator.
            services.RemoveAll<IIdempotencyStore>();
            services.AddCosmosInbox<CosmosProbeDbContext>();
        });
    }
}
