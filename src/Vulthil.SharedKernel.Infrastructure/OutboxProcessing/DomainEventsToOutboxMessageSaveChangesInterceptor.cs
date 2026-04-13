using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// EF Core save-changes interceptor that captures domain events from tracked aggregate roots
/// and persists them as <see cref="OutboxMessage"/> entries before the main save completes.
/// </summary>
public sealed class DomainEventsToOutboxMessageSaveChangesInterceptor(TimeProvider timeProvider, IOptions<OutboxProcessingOptions> outboxProcessingOptions) : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Captures domain events from tracked aggregate roots and stores them as outbox messages before persisting changes.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;

        if (dbContext is not ISaveOutboxMessages dbContextWithOutboxMessages)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        Activity? activity = null;

        if (outboxProcessingOptions.Value.EnableTracing)
        {
            activity = Activity.Current;
        }

        var groupId = Guid.CreateVersion7();

        var outboxMessages = dbContext.ChangeTracker.Entries<IAggregateRoot>()
            .Select(x => x.Entity)
            .SelectMany(aggregateRoot =>
            {
                var domainEvents = aggregateRoot.DomainEvents;

                aggregateRoot.ClearDomainEvents();

                return domainEvents;
            })
            .Select(d => new OutboxMessage
            {
                GroupId = groupId,
                OccurredOnUtc = _timeProvider.GetUtcNow(),
                Type = d.GetType().FullName!,
                Content = JsonSerializer.Serialize(d, d.GetType()),
                TraceParent = activity?.Id,
                TraceState = activity?.TraceStateString
            }).ToList();

        dbContextWithOutboxMessages.OutboxMessages.AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
