using System.Collections.Concurrent;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Messaging.DomainEvents;

internal sealed class DomainEventPublisher(IServiceProvider serviceProvider) : IDomainEventPublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private static readonly ConcurrentDictionary<Type, INotificationHandlerWrapper> _notificationHandlers = new();

    public Task PublishAsync(object notification, CancellationToken cancellationToken = default) =>
        notification switch
        {
            IDomainEvent instance => PublishAsync(instance, cancellationToken),
            null => throw new ArgumentNullException(nameof(notification)),
            _ => throw new ArgumentException($"{nameof(notification)} does not implement ${nameof(IDomainEvent)}")
        };
    public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : IDomainEvent =>
        InternalPublish(notification, cancellationToken);
    private Task InternalPublish<TNotification>(TNotification notification, CancellationToken cancellationToken) where TNotification : IDomainEvent
    {

        var handler = _notificationHandlers.GetOrAdd(notification.GetType(), static notificationType =>
        {
            var wrapperType = typeof(NotificationHandlerWrapper<>).MakeGenericType(notificationType);
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
            return (INotificationHandlerWrapper)wrapper;
        });

        return handler.HandleAsync(notification, _serviceProvider, PublishCore, cancellationToken);
    }

    private static async Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors, IDomainEvent notification, CancellationToken cancellationToken)
    {
        foreach (var executor in handlerExecutors)
        {
            await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
