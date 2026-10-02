using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging.DomainEvents;

/// <summary>
/// Dispatches a domain event of one runtime type; <see cref="DomainEventPublisher"/> keeps one per type.
/// </summary>
internal interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="domainEvent"/> to the handlers registered for its type.
    /// </summary>
    /// <param name="domainEvent">The domain event, of the dispatcher's event type.</param>
    /// <param name="services">The scope's service provider, which the handlers and pipeline handlers resolve from.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    Task DispatchAsync(IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches domain events of type <typeparamref name="TEvent"/>: resolves the event's handlers and pipeline handlers
/// from the scope, runs the pipeline handlers in registration order around the whole set of handlers, and runs the
/// handlers in registration order.
/// </summary>
/// <remarks>
/// A failed handler does not stop the others: once every handler has run, their failures are thrown together in one
/// <see cref="AggregateException"/>, even when only one handler failed. A cancellation of the caller's token stops the
/// dispatch instead: no further handler starts, and the <see cref="OperationCanceledException"/> propagates unwrapped,
/// even after an earlier handler failed, because the caller abandons the whole publish. A handler's own
/// <see cref="OperationCanceledException"/> while the caller's token is still live is an ordinary failure.
/// </remarks>
/// <typeparam name="TEvent">The domain event type.</typeparam>
internal sealed class DomainEventDispatcher<TEvent> : IDomainEventDispatcher
    where TEvent : IDomainEvent
{
    /// <inheritdoc />
    public Task DispatchAsync(IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken)
    {
        var typedEvent = (TEvent)domainEvent;
        var handlers = services.GetServices<IDomainEventHandler<TEvent>>();

        DomainEventPipelineDelegate pipeline = token => RunHandlersAsync(typedEvent, handlers, token);
        var pipelineHandlers = services.GetServices<IDomainEventPipelineHandler<TEvent>>().ToArray();
        for (var i = pipelineHandlers.Length - 1; i >= 0; i--)
        {
            var pipelineHandler = pipelineHandlers[i];
            var next = pipeline;
            pipeline = token => pipelineHandler.HandleAsync(typedEvent, next, token);
        }

        return pipeline(cancellationToken);
    }

    private static async Task RunHandlersAsync(TEvent domainEvent, IEnumerable<IDomainEventHandler<TEvent>> handlers, CancellationToken cancellationToken)
    {
        List<Exception>? failures = null;

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("One or more domain event handlers failed.", failures);
        }
    }
}
