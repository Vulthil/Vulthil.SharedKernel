using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// The public <see cref="IPublisher"/> facade. It builds the publish context, assigns a stable message id, runs the
/// registered <see cref="IPublishFilter"/> pipeline (resolved from the caller's scope), and delegates to the raw
/// <see cref="ITransportPublisher"/> terminal. Registered as scoped so filters can depend on scoped services.
/// </summary>
internal sealed class FilteringPublisher(IServiceProvider serviceProvider, ITransportPublisher transport) : IPublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => PublishAsync(message, null, cancellationToken);

    public async Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new PublishContext();
        if (configureContext is not null)
        {
            await configureContext(context);
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
            Kind = PublishKind.Publish,
            CancellationToken = cancellationToken,
        };

        var pipeline = PublishPipelineFactory.Build(
            serviceProvider,
            _ => transport.PublishAsync(
                message,
                target =>
                {
                    PublishContextCopier.CopyResolved(context, target);
                    return ValueTask.CompletedTask;
                },
                cancellationToken));

        await pipeline(filterContext);
    }
}
