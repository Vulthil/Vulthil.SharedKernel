using Microsoft.EntityFrameworkCore;
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

    public async Task ConsumeAsync(IMessageContext<MainEntityCreatedIntegrationEvent> messageContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received MainEntityCreatedIntegrationEvent with Id: {Id} and RoutingKey: {RoutingKey}", messageContext.Message.Id, messageContext.RoutingKey);
        var sideEffect = SideEffect.Create(messageContext.Message.Id, _timeProvider.GetUtcNow());

        _webApiDbContext.SideEffects.Add(sideEffect);

        await _webApiDbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SideEffectRequestConsumer(ILogger<MainEntityCreatedIntegrationEventConsumer> logger, IWebApiDbContext webApiDbContext) : IRequestConsumer<GetSideEffectsBelongingToMainEntity, List<SideEffectDto>>
{
    private readonly ILogger<MainEntityCreatedIntegrationEventConsumer> _logger = logger;
    private readonly IWebApiDbContext _webApiDbContext = webApiDbContext;


    public async Task<List<SideEffectDto>> ConsumeAsync(IMessageContext<GetSideEffectsBelongingToMainEntity> messageContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received MainEntityCreatedIntegrationEvent with Id: {Id} and RoutingKey: {RoutingKey}", messageContext.Message.Id, messageContext.RoutingKey);

        var sideEffects = await _webApiDbContext.SideEffects.Where(x => x.MainEntityId == messageContext.Message.Id)
            .Select(x => SideEffectDto.FromModel(x))
            .ToListAsync(cancellationToken);

        return sideEffects;
    }
}

public sealed record GetSideEffectsBelongingToMainEntity(Guid Id);
