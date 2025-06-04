using Microsoft.Extensions.Logging;
using Vulthil.SharedKernel.Events;
using WebApi.Domain.WebApiEntityModel.Events;

namespace WebApi.Application.WebApiEntity;

public sealed class WebApiEntityNameUpdatedEventHandler(ILogger<WebApiEntityNameUpdatedEventHandler> logger) : IDomainEventHandler<WebApiEntityNameUpdatedEvent>
{
    private readonly ILogger<WebApiEntityNameUpdatedEventHandler> _logger = logger;

    public Task HandleAsync(WebApiEntityNameUpdatedEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebApiEntity Name updated to: {Name}", notification.Name);
        return Task.CompletedTask;
    }
}
