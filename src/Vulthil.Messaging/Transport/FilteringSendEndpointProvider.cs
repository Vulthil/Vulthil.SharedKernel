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
/// Wraps a transport <see cref="ISendEndpoint"/>: builds the publish context, assigns a stable message id, runs the
/// registered <see cref="IPublishFilter"/> pipeline (as <see cref="PublishKind.Send"/>), and delegates to the inner
/// endpoint.
/// </summary>
internal sealed class FilteringSendEndpoint(IServiceProvider serviceProvider, ISendEndpoint inner) : ISendEndpoint
{
    public Uri Address => inner.Address;

    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => SendAsync(message, null, cancellationToken);

    public async Task SendAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
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
            Kind = PublishKind.Send,
            DestinationAddress = inner.Address,
            CancellationToken = cancellationToken,
        };

        var pipeline = PublishPipelineFactory.Build(
            serviceProvider,
            _ => inner.SendAsync(
                message,
                target =>
                {
                    PublishContextCopier.CopyResolved(context, target);
                    return ValueTask.CompletedTask;
                },
                cancellationToken));

        await pipeline(filterContext).ConfigureAwait(false);
    }
}
