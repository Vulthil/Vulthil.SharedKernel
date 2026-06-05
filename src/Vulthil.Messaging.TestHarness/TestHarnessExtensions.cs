using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// Registers the in-memory <see cref="ITestHarness"/> transport. The harness reuses the messaging configuration
/// (queues, consumers, message settings) that <c>AddMessaging</c> registered, so it mirrors the real topology
/// with no broker.
/// </summary>
public static class TestHarnessExtensions
{
    /// <summary>
    /// Configures the messaging infrastructure to use the in-memory test harness as its transport. Call this in
    /// place of a broker transport (e.g. <c>UseRabbitMq</c>) when composing messaging for a test.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public static IMessagingConfigurator UseTestHarness(this IMessagingConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        RegisterTestHarness(configurator.HostApplicationBuilder.Services);
        return configurator;
    }

    /// <summary>
    /// Replaces an already-registered transport (e.g. RabbitMQ) with the in-memory test harness, leaving the rest
    /// of the application's composition untouched. Call this from a test host's service-configuration hook (for
    /// example a <c>WebApplicationFactory</c>) so production code is not modified for tests. The orphaned broker
    /// registrations remain but are never resolved, so no broker connection is attempted.
    /// </summary>
    /// <param name="services">The service collection whose transport registrations are replaced.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection ReplaceTransportWithTestHarness(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterTestHarness(services);
        return services;
    }

    private static void RegisterTestHarness(IServiceCollection services)
    {
        services.RemoveAll<ITransport>();
        services.RemoveAll<IPublisher>();
        services.RemoveAll<ISendEndpointProvider>();
        services.RemoveAll<IRequester>();

        services.AddSingleton<TestHarness>();
        services.AddSingleton<ITestHarness>(sp => sp.GetRequiredService<TestHarness>());
        services.AddSingleton<InMemoryTransport>();
        services.AddSingleton<ITransport>(sp => sp.GetRequiredService<InMemoryTransport>());
        services.AddSingleton<IPublisher, InMemoryPublisher>();
        services.AddSingleton<ISendEndpointProvider, InMemorySendEndpointProvider>();
        services.AddSingleton<IRequester, InMemoryRequester>();
    }
}
