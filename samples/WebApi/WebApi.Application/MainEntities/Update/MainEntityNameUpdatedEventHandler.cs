using Microsoft.Extensions.Logging;
using Vulthil.SharedKernel.Events;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Application.MainEntities.Update;

/// <summary>
/// Represents the MainEntityNameUpdatedEventHandler.
/// </summary>
public sealed class MainEntityNameUpdatedEventHandler(ILogger<MainEntityNameUpdatedEventHandler> logger) : IDomainEventHandler<MainEntityNameUpdatedEvent>
{
    private readonly ILogger<MainEntityNameUpdatedEventHandler> _logger = logger;

    /// <summary>
    /// Executes this member.
    /// </summary>
    public Task HandleAsync(MainEntityNameUpdatedEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebApiEntity Name updated to: {Name}", notification.Name);
        return Task.CompletedTask;
    }
}
