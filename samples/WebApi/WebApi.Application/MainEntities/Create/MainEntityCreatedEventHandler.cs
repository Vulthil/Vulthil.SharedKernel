using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.SharedKernel.Events;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Application.MainEntities.Create;

/// <summary>
/// Represents the MainEntityCreatedEventHandler.
/// </summary>
public sealed class MainEntityCreatedEventHandler(ILogger<MainEntityCreatedEventHandler> logger, IPublisher publisher) : IDomainEventHandler<MainEntityCreatedEvent>
{
    private readonly ILogger<MainEntityCreatedEventHandler> _logger = logger;
    private readonly IPublisher _publisher = publisher;

    /// <summary>
    /// Executes this member.
    /// </summary>
    public async Task HandleAsync(MainEntityCreatedEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MainEntityCreated: {Id}", notification.Id);
        await _publisher.PublishAsync(new MainEntityCreatedIntegrationEvent(notification.Id.Value), cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Represents the MainEntityCreatedIntegrationEvent.
/// </summary>
public sealed record MainEntityCreatedIntegrationEvent(Guid Id);
