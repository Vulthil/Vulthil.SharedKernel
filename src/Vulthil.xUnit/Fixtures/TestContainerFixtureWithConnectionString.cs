using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Fixture that wraps a Testcontainers container and exposes a connection string for integration test configuration.
/// </summary>
public abstract class TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : TestContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestContainerWithConnectionString
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
    /// <summary>
    /// Gets the connection string used to communicate with the containerized service.
    /// </summary>
    public abstract string ConnectionString { get; }
    /// <summary>
    /// Gets the configuration key name where the connection string should be injected.
    /// </summary>
    public abstract string ConnectionStringKey { get; }
}
