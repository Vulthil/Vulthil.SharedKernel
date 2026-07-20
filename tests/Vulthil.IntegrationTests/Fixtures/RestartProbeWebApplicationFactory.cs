using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Boots the test host against the shared PostgreSQL server only (the broker transport is swapped for the in-memory
/// harness, same as <see cref="TestHarnessWebApplicationFactory"/>), and additionally registers a
/// <see cref="RestartableProbe"/> that records every start/stop it observes into <see cref="Events"/>. Used to verify
/// that resetting after a test pauses and resumes the host the test actually ran on, not an unrelated, never-built
/// clone of this fixture.
/// </summary>
public sealed class RestartProbeWebApplicationFactory(IntegrationTestContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    /// <summary>
    /// Gets the shared log of "start"/"stop" events recorded by every <see cref="RestartableProbe"/> this factory's
    /// hosts (its own, or any <c>WithWebHostBuilder</c> clone of it) have registered.
    /// </summary>
    public ConcurrentQueue<string> Events { get; } = new();

    protected override bool ShouldUseContainer(ITestContainer container) => container is PostgreSqlTestContainer;

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // No broker container is consumed, so stub the connection string to satisfy the RabbitMQ client registration;
        // swapping the transport means the broker connection is never resolved.
        builder.UseSetting($"ConnectionStrings:{TestHostConnectionStrings.RabbitMq}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services =>
        {
            services.ReplaceTransportWithTestHarness();
            services.AddSingleton<IHostedService>(new RestartableProbe(Events));
        });
    }
}
