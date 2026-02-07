using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using WebApi.Application.MainEntities.Create;
using WebApi.Domain.SideEffects;

namespace WebApi.Application.SideEffects.Create;

public sealed class MainEntityCreatedIntegrationEventConsumer(ILogger<MainEntityCreatedIntegrationEventConsumer> logger, IWebApiDbContext webApiDbContext, TimeProvider timeProvider) : IConsumer<MainEntityCreatedIntegrationEvent>
{
    private readonly ILogger<MainEntityCreatedIntegrationEventConsumer> _logger = logger;
    private readonly IWebApiDbContext _webApiDbContext = webApiDbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task ConsumeAsync(IMessageContext<MainEntityCreatedIntegrationEvent> message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received MainEntityCreatedIntegrationEvent with Id: {Id} and RoutingKey: {RoutingKey}", message.Message.Id, message.RoutingKey);

        var sideEffect = SideEffect.Create(message.Message.Id, _timeProvider.GetUtcNow());

        _webApiDbContext.SideEffects.Add(sideEffect);

        await _webApiDbContext.SaveChangesAsync(cancellationToken);
    }
}
