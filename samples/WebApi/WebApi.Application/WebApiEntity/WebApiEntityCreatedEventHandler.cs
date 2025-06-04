using Microsoft.Extensions.Logging;
using Vulthil.SharedKernel.Events;
using WebApi.Domain.WebApiEntityModel.Events;

namespace WebApi.Application.WebApiEntity;

public sealed class WebApiEntityCreatedEventHandler(ILogger<WebApiEntityCreatedEventHandler> logger) : IDomainEventHandler<WebApiEntityCreatedEvent>
{
    private readonly ILogger<WebApiEntityCreatedEventHandler> _logger = logger;

    public Task HandleAsync(WebApiEntityCreatedEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebApiEntityCreated: {Id}", notification.Id.Value);
        return Task.CompletedTask;
    }

}
