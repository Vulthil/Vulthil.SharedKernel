using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// EF Core save-changes interceptor that captures domain events from tracked aggregate roots and persists them as
/// <see cref="OutboxMessage"/> entries before the main save completes. It also reports every save to
/// <see cref="OutboxRelayWakeup"/>, which wakes the outbox relay once the outbox rows the save inserted are durable.
/// </summary>
public sealed class DomainEventsToOutboxMessageSaveChangesInterceptor(TimeProvider timeProvider, IOptions<OutboxProcessingOptions> outboxProcessingOptions, OutboxRelayWakeup relayWakeup) : SaveChangesInterceptor, IOutboxInterceptor
{
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Captures domain events from tracked aggregate roots and stores them as outbox messages before persisting
    /// changes, then records whether the save inserts outbox rows.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        OnSavingChanges(eventData.Context);
        return result;
    }

    /// <summary>
    /// Captures domain events from tracked aggregate roots and stores them as outbox messages before persisting
    /// changes, then records whether the save inserts outbox rows.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        OnSavingChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Reports the completed save to <see cref="OutboxRelayWakeup"/>: outbox rows it inserted outside a transaction
    /// wake the relay now, and rows it inserted inside a transaction wake it when that transaction commits.
    /// </summary>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        OnSavedChanges(eventData.Context);
        return result;
    }

    /// <summary>
    /// Reports the completed save to <see cref="OutboxRelayWakeup"/>: outbox rows it inserted outside a transaction
    /// wake the relay now, and rows it inserted inside a transaction wake it when that transaction commits.
    /// </summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        OnSavedChanges(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void OnSavingChanges(DbContext? dbContext)
    {
        if (dbContext is not ISaveOutboxMessages dbContextWithOutboxMessages)
        {
            return;
        }

        CaptureDomainEvents(dbContext, dbContextWithOutboxMessages);
        relayWakeup.SavingChanges(dbContext);
    }

    private void OnSavedChanges(DbContext? dbContext)
    {
        if (dbContext is ISaveOutboxMessages)
        {
            relayWakeup.SavedChanges(dbContext);
        }
    }

    private void CaptureDomainEvents(DbContext dbContext, ISaveOutboxMessages dbContextWithOutboxMessages)
    {
        Activity? activity = null;

        if (outboxProcessingOptions.Value.EnableTracing)
        {
            activity = Activity.Current;
        }

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
                OccurredOnUtc = _timeProvider.GetUtcNow(),
                Type = d.GetType().FullName!,
                Content = JsonSerializer.Serialize(d, d.GetType()),
                TraceParent = activity?.Id,
                TraceState = activity?.TraceStateString,
                Destination = OutboxDestination.DomainEvent
            }).ToList();

        dbContextWithOutboxMessages.OutboxMessages.AddRange(outboxMessages);
    }
}
