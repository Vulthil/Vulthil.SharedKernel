using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.Data;

public sealed class DomainEventsToOutboxMessageSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider = timeProvider;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;

        if (dbContext is null || dbContext is not ISaveOutboxMessages dbContextWithOutboxMessages)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
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
                Type = d.GetType().AssemblyQualifiedName!,
                Content = JsonSerializer.Serialize(d, d.GetType())
            }).ToList();

        dbContextWithOutboxMessages.OutboxMessages.AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
