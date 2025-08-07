using Vulthil.Messaging.Abstractions.Consumers;
using WebApi.Application.MainEntities.Create;
using WebApi.Domain.SideEffects;

namespace WebApi.Application.SideEffects.Create;

public sealed class MainEntityCreatedIntegrationEventConsumer(IWebApiDbContext webApiDbContext, TimeProvider timeProvider) : IConsumer<MainEntityCreatedIntegrationEvent>
{
    private readonly IWebApiDbContext _webApiDbContext = webApiDbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task ConsumeAsync(MainEntityCreatedIntegrationEvent message, CancellationToken cancellationToken = default)
    {
        var sideEffect = SideEffect.Create(message.Id, _timeProvider.GetUtcNow());

        _webApiDbContext.SideEffects.Add(sideEffect);

        await _webApiDbContext.SaveChangesAsync(cancellationToken);
    }
}
