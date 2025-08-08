
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

public abstract class TestContainerFixture<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : ContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestContainer
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>, new()
    where TContainerEntity : IContainer
{
    protected override ValueTask InitializeAsync() => base.InitializeAsync();
    protected override ValueTask DisposeAsyncCore() => base.DisposeAsyncCore();
}
