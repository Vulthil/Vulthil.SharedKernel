using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.TestHost.Data;

namespace Vulthil.TestHost.Probes;

/// <summary>
/// Integration event used by the integration tests to exercise the publish → consume pipeline. The host's inbox is
/// keyed on <see cref="Id"/>, so two publishes with the same id are duplicates of one logical event.
/// </summary>
public sealed record ProbeCreatedIntegrationEvent(Guid Id);

/// <summary>
/// Row written by <see cref="ProbeCreatedIntegrationEventConsumer"/>; its presence (and count) is the observable
/// proof that the consumer ran.
/// </summary>
public sealed class ProbeSideEffect
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProbeId { get; init; }
}

public sealed record ProbeSideEffectDto(Guid Id, Guid ProbeId);

/// <summary>
/// Request handled by <see cref="GetProbeSideEffectsConsumer"/>, returning the side effects recorded for a probe.
/// </summary>
public sealed record GetProbeSideEffects(Guid ProbeId);

public sealed class ProbeCreatedIntegrationEventConsumer(TestHostDbContext dbContext) : IConsumer<ProbeCreatedIntegrationEvent>
{
    public async Task ConsumeAsync(IMessageContext<ProbeCreatedIntegrationEvent> messageContext, CancellationToken cancellationToken = default)
    {
        dbContext.ProbeSideEffects.Add(new ProbeSideEffect { ProbeId = messageContext.Message.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class GetProbeSideEffectsConsumer(TestHostDbContext dbContext) : IRequestConsumer<GetProbeSideEffects, List<ProbeSideEffectDto>>
{
    public async Task<List<ProbeSideEffectDto>> ConsumeAsync(IMessageContext<GetProbeSideEffects> messageContext, CancellationToken cancellationToken = default) =>
        await dbContext.ProbeSideEffects
            .Where(sideEffect => sideEffect.ProbeId == messageContext.Message.ProbeId)
            .Select(sideEffect => new ProbeSideEffectDto(sideEffect.Id, sideEffect.ProbeId))
            .ToListAsync(cancellationToken);
}
