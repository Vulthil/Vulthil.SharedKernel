using Microsoft.AspNetCore.Hosting;
using ServiceDefaults;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Boots the WebApi sample with its production messaging composition, then swaps the broker transport for the
/// in-memory test harness — demonstrating an integration test that runs the real consumers with a real database
/// but no message broker. Only a PostgreSQL container is started.
/// </summary>
public sealed class TestHarnessWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public TestHarnessWebApplicationFactory(IMessageSink messageSink)
        => AddContainer(new PostgreSqlTestContainer(messageSink));

    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        // No broker container runs, so stub the connection string to satisfy the RabbitMQ client registration;
        // swapping the transport means the broker connection is never resolved.
        builder.UseSetting($"ConnectionStrings:{ServiceNames.RabbitMqServiceName}", "amqp://guest:guest@localhost:5672");
        builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
    }

}
