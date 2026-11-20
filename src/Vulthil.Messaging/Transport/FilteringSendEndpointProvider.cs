using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// The public <see cref="ISendEndpointProvider"/> facade. It resolves the raw transport endpoint and wraps it with
/// a <see cref="FilteringSendEndpoint"/> so sends run through the publish pipeline. Registered as scoped so filters
/// can depend on scoped services.
/// </summary>
internal sealed class FilteringSendEndpointProvider(IServiceProvider serviceProvider, ITransportSendEndpointProvider transport) : ISendEndpointProvider
{
    public async ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default)
    {
        var endpoint = await transport.GetSendEndpointAsync(address, cancellationToken).ConfigureAwait(false);
        return new FilteringSendEndpoint(serviceProvider, endpoint);
    }
}

/// <summary>
/// Wraps a transport <see cref="ITransportSendEndpoint"/>: runs the message through the registered
/// <see cref="IPublishFilter"/> pipeline (as <see cref="PublishKind.Send"/>, see <see cref="PublishPipelineFactory"/>)
/// and hands the message and the resolved context to the inner endpoint.
/// </summary>
internal sealed class FilteringSendEndpoint(IServiceProvider serviceProvider, ITransportSendEndpoint inner) : ISendEndpoint
{
    public Uri Address => inner.Address;

    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => SendAsync(message, null, cancellationToken);

    public Task SendAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
        => PublishPipelineFactory.RunAsync(
            serviceProvider,
            message,
            PublishKind.Send,
            inner.Address,
            configureContext,
            context => inner.SendAsync(message, context, cancellationToken),
            cancellationToken);
}
