using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Runs a publish or a send through the registered <see cref="IPublishFilter"/> pipeline: builds the publish context,
/// assigns a stable message id, and composes the filters around the transport call. The filtering publisher and send
/// endpoint call this in their publish path. Internal on purpose: consumers extend the publish path by registering an
/// <see cref="IPublishFilter"/>, and a transport never composes this pipeline itself — the filtering wrappers
/// registered by <c>AddPublishFiltering</c> do.
/// </summary>
internal static class PublishPipelineFactory
{
    public static async Task RunAsync<TMessage>(
        IServiceProvider serviceProvider,
        TMessage message,
        PublishKind kind,
        Uri? destinationAddress,
        Func<IPublishContext, ValueTask>? configureContext,
        Func<PublishContext, Task> transport,
        CancellationToken cancellationToken)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new PublishContext();
        if (configureContext is not null)
        {
            await configureContext(context).ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(context.MessageId))
        {
            context.SetMessageId(Guid.CreateVersion7().ToString());
        }

        var filterContext = new PublishFilterContext
        {
            Message = message,
            MessageType = message.GetType(),
            Context = context,
            Kind = kind,
            DestinationAddress = destinationAddress,
            CancellationToken = cancellationToken,
        };

        var pipeline = Build(serviceProvider, _ => transport(context));
        await pipeline(filterContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Composes the registered <see cref="IPublishFilter"/> instances around <paramref name="terminal"/>. The first
    /// filter resolved from DI becomes the outermost; the terminal delegate runs innermost.
    /// </summary>
    private static PublishFilterDelegate Build(IServiceProvider serviceProvider, PublishFilterDelegate terminal)
    {
        var filters = serviceProvider.GetServices<IPublishFilter>().ToArray();
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
