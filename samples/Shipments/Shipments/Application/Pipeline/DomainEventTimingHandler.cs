using System.Diagnostics;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;

namespace Shipments.Application.Pipeline;

/// <summary>
/// A custom domain-event pipeline handler: it wraps every handler of every domain event and logs slow ones.
/// <c>Program.cs</c> registers it with <c>AddOpenDomainEventPipelineHandler</c>.
/// </summary>
public sealed class DomainEventTimingHandler<TDomainEvent>(
    TimeProvider timeProvider,
    ILogger<DomainEventTimingHandler<TDomainEvent>> logger) : IDomainEventPipelineHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    public async Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default)
    {
        var started = timeProvider.GetTimestamp();
        await next(cancellationToken);
        var elapsed = timeProvider.GetElapsedTime(started);

        if (elapsed > SlowThreshold)
        {
            logger.LogWarning("Handling {DomainEvent} took {Elapsed}", typeof(TDomainEvent).Name, elapsed);
        }
    }
}
