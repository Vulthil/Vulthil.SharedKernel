using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;
using Vulthil.SharedKernel.xUnit.Containers;
using WebApi.ServiceDefaults;

namespace WebApi.Tests;

public sealed class RabbitMqPod : ContainerWithConnectionStringPool<RabbitMqBuilder, RabbitMqContainer>
{
    private readonly RabbitMqBuilder _rabbitMqContainer = new RabbitMqBuilder()
        .WithUsername("guest")
        .WithPassword("guest");
    protected override int PoolSize => 1;
    public override string KeyName => ServiceNames.RabbitMqServiceName;

    protected override IContainerBuilder<RabbitMqBuilder, RabbitMqContainer> ContainerBuilder => _rabbitMqContainer;

    public override string GetConnectionString(RabbitMqContainer container) => container.GetConnectionString();
}
