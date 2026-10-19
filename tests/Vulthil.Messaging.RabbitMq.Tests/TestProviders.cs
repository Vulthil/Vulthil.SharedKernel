using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Builds a real <see cref="IMessageConfigurationProvider"/> through the public <c>AddMessaging</c> entry point,
/// so transport tests exercise the same provider the host wires up — without reaching into messaging internals.
/// The builder has no configuration sources: the default builder watches <c>appsettings.json</c> for changes, these
/// builders are never disposed, and Linux allows only a few file watchers per user (128 inotify instances on the CI
/// runners), so a test run that builds a provider per test would run out of them.
/// </summary>
internal static class TestProviders
{
    /// <summary>
    /// Builds a provider, optionally applying <paramref name="configure"/> to register queues, consumers, and partitions.
    /// </summary>
    /// <param name="configure">Optional messaging configuration callback.</param>
    /// <returns>The resolved configuration provider.</returns>
    public static IMessageConfigurationProvider Build(Action<IMessagingConfigurator>? configure = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddMessaging(configure ?? (_ => { }));
        using var serviceProvider = builder.Services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IMessageConfigurationProvider>();
    }
}
