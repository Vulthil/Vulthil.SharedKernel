using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// EF Core save-changes interceptor that captures domain events from tracked aggregate roots
/// and persists them as <see cref="OutboxMessage"/> entries before the main save completes.
/// </summary>
public sealed class DomainEventsToOutboxMessageSaveChangesInterceptor(TimeProvider timeProvider, IOptions<OutboxProcessingOptions> outboxProcessingOptions, IOutboxSignal signal) : SaveChangesInterceptor, IOutboxInterceptor
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

#if NET10_0_OR_GREATER
        var groupId = Guid.CreateVersion7();
#else
        var groupId = Guid.NewGuid();
#endif

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
                TraceState = activity?.TraceStateString,
                Destination = OutboxDestination.DomainEvent
            }).ToList();

        dbContextWithOutboxMessages.OutboxMessages.AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Wakes the outbox relay after a save that committed outside an explicit transaction, so domain events captured
    /// by a bare <c>SaveChanges</c> are relayed promptly instead of waiting for the next poll. When a transaction is
    /// open the relay is signalled on commit by the transaction-commit interceptor instead, so this skips that case to
    /// avoid waking the relay before the rows are committed and visible.
    /// </summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (ShouldWakeRelay(eventData.Context))
        {
            signal.Notify();
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static bool ShouldWakeRelay(DbContext? dbContext) =>
        dbContext is ISaveOutboxMessages
        && dbContext.Database.CurrentTransaction is null
        && dbContext.ChangeTracker.Entries<IAggregateRoot>().Any()
        && dbContext.ChangeTracker.Entries<OutboxMessage>().Any(entry => entry.Entity.ProcessedOnUtc is null);
}
