using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// The public <see cref="IPublisher"/> facade. It runs the message through the registered <see cref="IPublishFilter"/>
/// pipeline (resolved from the caller's scope, see <see cref="PublishPipelineFactory"/>) and hands the message and the
/// resolved context to the raw <see cref="ITransportPublisher"/> terminal. Registered as scoped so filters can depend on
/// scoped services.
/// </summary>
internal sealed class FilteringPublisher(IServiceProvider serviceProvider, ITransportPublisher transport) : IPublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => PublishAsync(message, null, cancellationToken);

    public Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
        => PublishPipelineFactory.RunAsync(
            serviceProvider,
            message,
            PublishKind.Publish,
            destinationAddress: null,
            configureContext,
            context => transport.PublishAsync(message, context, cancellationToken),
            cancellationToken);
}
