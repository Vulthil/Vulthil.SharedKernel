using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Fixture that wraps a RabbitMQ broker container. When owned by a <see cref="ContainerHost"/>, every consuming
/// factory gets its own virtual host on the shared broker (created via <c>rabbitmqctl</c> inside the container and
/// appended to the AMQP connection string), so parallel test classes never see each other's exchanges, queues or
/// messages. Derived classes only configure the container and supply the connection string and key.
/// </summary>
public abstract class RabbitMqTestContainerFixture<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>(messageSink)
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
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
        string virtualHost) : ITestContainerWithConnectionString
    {
        public string ConnectionString => fixture.BuildScopedConnectionString(virtualHost);

        public string ConnectionStringKey => fixture.ConnectionStringKey;

        public void ConfigureWebHost(IWebHostBuilder builder) => fixture.ConfigureWebHost(builder);

        public void ConfigureServices(IServiceCollection services) => fixture.ConfigureServices(services);

        public async ValueTask InitializeAsync()
        {
            await fixture.ExecuteBrokerCommandAsync("rabbitmqctl", "add_vhost", virtualHost).ConfigureAwait(false);
            await fixture.ExecuteBrokerCommandAsync("rabbitmqctl", "set_permissions", "-p", virtualHost, fixture.VirtualHostUsername, ".*", ".*", ".*").ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await fixture.ExecuteBrokerCommandAsync("rabbitmqctl", "delete_vhost", virtualHost).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                TestContext.Current.SendDiagnosticMessage(
                    $"Deleting virtual host '{virtualHost}' failed; it is removed with the container: {exception.Message}");
            }
        }
    }
}
