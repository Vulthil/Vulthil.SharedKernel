using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Composes the registered <see cref="IPublishFilter"/> instances around a terminal delegate (the transport send).
/// The filtering publisher/send-endpoint call this in their publish path to wrap the transport with the configured
/// middleware.
/// </summary>
public static class PublishPipelineFactory
{
    /// <summary>
    /// Composes the registered <see cref="IPublishFilter"/> instances around <paramref name="terminal"/>. The first
    /// filter resolved from DI becomes the outermost; the terminal delegate runs innermost.
    /// </summary>
    /// <param name="serviceProvider">The scope's service provider used to resolve the filters.</param>
    /// <param name="terminal">The innermost delegate, typically the transport publish/send.</param>
    /// <returns>A delegate that runs all filters in order and then the terminal.</returns>
    public static PublishFilterDelegate Build(IServiceProvider serviceProvider, PublishFilterDelegate terminal)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(terminal);

        var filters = serviceProvider.GetServices<IPublishFilter>().ToArray();
        if (filters.Length == 0)
        {
            return terminal;
        }

        var pipeline = terminal;
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var next = pipeline;
            pipeline = context => filter.PublishAsync(context, next);
        }

        return pipeline;
    }
}
