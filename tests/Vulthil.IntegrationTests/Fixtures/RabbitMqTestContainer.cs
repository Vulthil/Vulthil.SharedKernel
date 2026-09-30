using Testcontainers.RabbitMq;
using Vulthil.TestHost;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests.Fixtures;

internal sealed class RabbitMqTestContainer : RabbitMqTestContainerFixture<RabbitMqBuilder, RabbitMqContainer>
{
    private readonly RabbitMqBuilder _builder = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("guest")
        .WithPassword("guest");

    protected override RabbitMqBuilder Configure() => _builder;

    public override string ConnectionStringKey => TestHostConnectionStrings.RabbitMq;

    public override string ConnectionString => Container.GetConnectionString();
}
