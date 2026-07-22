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

internal sealed record NotificationHandlerExecutor(Func<IDomainEvent, CancellationToken, Task> HandlerCallback);

internal sealed class NotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
    where TNotification : IDomainEvent
{
    /// <inheritdoc />
    public Task HandleAsync(IDomainEvent domainEvent, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, IDomainEvent, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var handlers = serviceFactory
            .GetServices<IDomainEventHandler<TNotification>>()
            .Select(static x => new NotificationHandlerExecutor((n, ct) => x.HandleAsync((TNotification)n, ct)));

        Task Handlers(CancellationToken t = default) => publish(handlers, domainEvent, t);

        var pipelineHandlers = serviceFactory.GetServices<IDomainEventPipelineHandler<TNotification>>().ToArray();

        DomainEventPipelineDelegate h = Handlers;
        for (var i = pipelineHandlers.Length - 1; i >= 0; i--)
        {
            var pipelineHandler = pipelineHandlers[i];
            var next = h;
            h = t => pipelineHandler.HandleAsync((TNotification)domainEvent, next, t);
        }

        return h(cancellationToken);
    }
}
