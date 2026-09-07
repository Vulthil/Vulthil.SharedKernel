using Vulthil.xUnit.Fixtures;
using WebApi.Tests.Fixtures;

[assembly: AssemblyFixture<AppContainerHost>]

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Assembly-wide container host: one PostgreSQL server and one RabbitMQ broker are started at most once for the
/// whole test run. Factories consume them through per-class scopes (a uniquely named database and a virtual host),
/// so test classes run in parallel against shared containers without interfering.
/// </summary>
public sealed class AppContainerHost : ContainerHost
{
    protected override Task ConfigureContainers()
    {
        AddContainer<PostgreSqlTestContainer>();
        AddContainer<RabbitMqTestContainer>();
        return Task.CompletedTask;
    }
}
