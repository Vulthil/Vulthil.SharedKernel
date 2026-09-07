using ServiceDefaults;
using Testcontainers.RabbitMq;
using Vulthil.xUnit.Fixtures;

namespace WebApi.Tests.Fixtures;

internal sealed class RabbitMqTestContainer : RabbitMqTestContainerFixture<RabbitMqBuilder, RabbitMqContainer>
{
    private readonly RabbitMqBuilder _builder = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("guest")
        .WithPassword("guest");
    protected override RabbitMqBuilder Configure() => _builder;

    public override string ConnectionStringKey => ServiceNames.RabbitMqServiceName;
    public override string ConnectionString => Container.GetConnectionString();
}
