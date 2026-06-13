using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Registers the publish/send filtering facade in front of a transport's raw publish terminal. A transport
/// registers its publisher and send-endpoint provider under <see cref="ITransportPublisher"/> /
/// <see cref="ITransportSendEndpointProvider"/> and then calls <see cref="AddPublishFiltering"/> so the public
/// <see cref="IPublisher"/> / <see cref="ISendEndpointProvider"/> run every publish and send through the pipeline.
/// </summary>
public static class PublishFilteringServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scoped filtering <see cref="IPublisher"/> and <see cref="ISendEndpointProvider"/> over the
    /// transport's <see cref="ITransportPublisher"/> / <see cref="ITransportSendEndpointProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddPublishFiltering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IPublisher, FilteringPublisher>();
        services.AddScoped<ISendEndpointProvider, FilteringSendEndpointProvider>();

        return services;
    }
}
