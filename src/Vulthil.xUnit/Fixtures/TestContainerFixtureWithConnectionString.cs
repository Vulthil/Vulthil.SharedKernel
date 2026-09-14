using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Fixture that wraps a Testcontainers container and exposes a connection string for integration test configuration.
/// </summary>
public abstract class TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>
    : TestContainerFixture<TBuilderEntity, TContainerEntity>, ITestContainerWithConnectionString
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
    /// <summary>
    /// Initializes the fixture. Container logs go to <paramref name="messageSink"/> when one is given, otherwise to
    /// the current test context's diagnostic messages, so nothing has to be injected.
    /// </summary>
    /// <param name="messageSink">
    /// The xUnit diagnostic message sink for container logs, or <see langword="null"/> to use the current test
    /// context's diagnostic messages.
    /// </param>
    protected TestContainerFixtureWithConnectionString(IMessageSink? messageSink = null)
        : base(messageSink)
    {
    }

    /// <summary>
    /// Gets the connection string used to communicate with the containerized service.
    /// </summary>
    public abstract string ConnectionString { get; }
    /// <summary>
    /// Gets the configuration key name where the connection string should be injected.
    /// </summary>
    public abstract string ConnectionStringKey { get; }

    /// <summary>
    /// Creates a pass-through scope view over this container that forwards its connection string unchanged, so every
    /// consumer shares the container's namespace. Override to return a view that is isolated under
    /// <paramref name="scopeId"/> when the containerized service supports namespacing (for example one virtual host
    /// per scope on a message broker).
    /// </summary>
    /// <param name="scopeId">A short, unique, lowercase identifier for the scope, safe to embed in names.</param>
    /// <returns>The scoped container view.</returns>
    public override ITestContainer CreateScope(string scopeId) => new TestContainerWithConnectionStringScope(this);
}
