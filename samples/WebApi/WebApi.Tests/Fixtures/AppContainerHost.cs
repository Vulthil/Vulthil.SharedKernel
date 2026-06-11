using Vulthil.xUnit.Fixtures;
using WebApi.Tests.Fixtures;
using Xunit.Sdk;

[assembly: AssemblyFixture(typeof(AppContainerHost))]

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Assembly-wide container host: one PostgreSQL server, one RabbitMQ broker and one Cosmos emulator are started at
/// most once for the whole test run. Factories consume them through per-class scopes (a uniquely named database, a
/// virtual host, a Cosmos database), so test classes run in parallel against shared containers without interfering.
/// Containers start lazily, so a filtered run only pays for what its factories consume.
/// </summary>
public sealed class AppContainerHost(IMessageSink messageSink) : ContainerHost(messageSink)
{
    protected override Task ConfigureContainers()
    {
        AddContainer(new PostgreSqlTestContainer(MessageSink));
        AddContainer(new RabbitMqTestContainer(MessageSink));
        AddContainer(new CosmosTestContainer(MessageSink));
        return Task.CompletedTask;
    }
}
