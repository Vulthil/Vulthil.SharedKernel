using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging.DomainEvents;

internal interface INotificationHandlerWrapper
{
    Task HandleAsync(IDomainEvent domainEvent, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, IDomainEvent, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}

internal sealed record NotificationHandlerExecutor(object HandlerInstance, Func<IDomainEvent, CancellationToken, Task> HandlerCallback);
internal sealed record PipelineHandlerExecutor(object HandlerInstance, Func<IDomainEvent, CancellationToken, Task> HandlerCallback);

internal sealed class NotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
    where TNotification : IDomainEvent
{
    public Task HandleAsync(IDomainEvent domainEvent, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, IDomainEvent, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var handlers = serviceFactory
            .GetServices<IDomainEventHandler<TNotification>>()
            .Select(static x => new NotificationHandlerExecutor(x, (n, ct) => x.HandleAsync((TNotification)n, ct)));

        Task Handlers(CancellationToken t = default) => publish(handlers, domainEvent, t);

        var h = serviceFactory
            .GetServices<IDomainEventPipelineHandler<TNotification>>()
            .Reverse()
            .Aggregate((DomainEventPipelineDelegate)Handlers,
                (next, pipeline) => (t) => pipeline.HandleAsync((TNotification)domainEvent, next, t));

        return h(cancellationToken);
    }
}
