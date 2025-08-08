using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

public sealed class FixtureWrapper : TestFixture
{
    public FixtureWrapper(IMessageSink messageSink) : base()
    {
        AddContainer(new PostgreSqlTestContainer(messageSink));
        AddContainer(new RabbitMqTestContainer(messageSink));
    }

    protected override Task ConfigureContainers()
    {
        return Task.CompletedTask;
    }
}
