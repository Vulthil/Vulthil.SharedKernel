using MediatR;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IIntegrationEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : INotification;
