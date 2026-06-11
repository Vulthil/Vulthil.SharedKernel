using Vulthil.xUnit.Fixtures;
using WebApi.Tests.Fixtures;
using Xunit.Sdk;

[assembly: AssemblyFixture(typeof(AppContainerHost))]

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Assembly-wide container host: one PostgreSQL server and one RabbitMQ broker are started at most once for the
/// whole test run. Factories consume them through per-class scopes (a uniquely named database and a virtual host),
/// so test classes run in parallel against shared containers without interfering.
/// </summary>
public sealed class AppContainerHost(IMessageSink messageSink) : ContainerHost(messageSink)
{
    protected override Task ConfigureContainers()
    {
        AddContainer(new PostgreSqlTestContainer(MessageSink));
        AddContainer(new RabbitMqTestContainer(MessageSink));
        return Task.CompletedTask;
    }
}
