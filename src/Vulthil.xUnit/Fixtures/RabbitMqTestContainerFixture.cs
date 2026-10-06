using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Fixture that wraps a RabbitMQ broker container. When owned by a <see cref="ContainerHost"/>, every consuming
/// factory gets its own virtual host on the shared broker (created via <c>rabbitmqctl</c> inside the container and
/// appended to the AMQP connection string), so parallel test classes never see each other's exchanges, queues or
/// messages. Derived classes only configure the container and supply the connection string and key.
/// </summary>
public abstract class RabbitMqTestContainerFixture<TBuilderEntity, TContainerEntity>
    : TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
    /// <summary>
    /// Initializes the fixture. Broker logs go to <paramref name="messageSink"/> when one is given, otherwise to the
    /// current test context's diagnostic messages, so nothing has to be injected.
    /// </summary>
    /// <param name="messageSink">
    /// The xUnit diagnostic message sink for broker logs, or <see langword="null"/> to use the current test
    /// context's diagnostic messages.
    /// </param>
    protected RabbitMqTestContainerFixture(IMessageSink? messageSink = null)
        : base(messageSink)
    {
    }

    /// <summary>
    /// Gets the broker username that is granted full permissions on each scope's virtual host. Must match the
    /// username the container was configured with; defaults to <c>guest</c>.
    /// </summary>
    protected virtual string VirtualHostUsername => "guest";

    /// <summary>
    /// Creates a scope backed by its own virtual host on this broker, named after <paramref name="scopeId"/>. The
    /// scope creates the virtual host when it initializes and deletes it (best-effort) when disposed; the broker
    /// container itself keeps running for other scopes.
    /// </summary>
    /// <param name="scopeId">A short, unique, lowercase identifier for the scope; used as the virtual host name.</param>
    /// <returns>The scoped container view.</returns>
    public override ITestContainer CreateScope(string scopeId) => new VirtualHostScope(this, scopeId);

    /// <summary>
    /// Builds the connection string targeting <paramref name="virtualHost"/> by appending it to the AMQP URI
    /// returned by <see cref="TestContainerFixtureWithConnectionString{TBuilderEntity, TContainerEntity}.ConnectionString"/>.
    /// Override when the connection string is not URI-shaped.
    /// </summary>
    /// <param name="virtualHost">The virtual host the returned connection string should target.</param>
    /// <returns>The scoped connection string.</returns>
    protected virtual string BuildScopedConnectionString(string virtualHost)
    {
        ArgumentException.ThrowIfNullOrEmpty(virtualHost);
        return $"{ConnectionString.TrimEnd('/')}/{Uri.EscapeDataString(virtualHost)}";
    }

    private async Task ExecuteBrokerCommandAsync(params string[] command)
    {
        ExecResult result = await Container.ExecAsync(command).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{string.Join(' ', command)}' failed with exit code {result.ExitCode}: {result.Stderr}");
        }
    }

    private sealed class VirtualHostScope(
        RabbitMqTestContainerFixture<TBuilderEntity, TContainerEntity> fixture,
        string virtualHost)
        : TestContainerWithConnectionStringScope<RabbitMqTestContainerFixture<TBuilderEntity, TContainerEntity>>(fixture, virtualHost)
    {
        private readonly string _virtualHost = virtualHost;

        public override string ConnectionString => Container.BuildScopedConnectionString(_virtualHost);

        protected override async ValueTask CreateNamespaceAsync(string namespaceName)
        {
            await Container.ExecuteBrokerCommandAsync("rabbitmqctl", "add_vhost", namespaceName).ConfigureAwait(false);
            await Container.ExecuteBrokerCommandAsync("rabbitmqctl", "set_permissions", "-p", namespaceName, Container.VirtualHostUsername, ".*", ".*", ".*").ConfigureAwait(false);
        }

        protected override ValueTask DeleteNamespaceAsync(string namespaceName) =>
            new(Container.ExecuteBrokerCommandAsync("rabbitmqctl", "delete_vhost", namespaceName));
    }
}
