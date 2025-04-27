using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.Data;

public static class DbContextExtensions
{
    public static async Task<int> SaveAndPublishChangesAsync(this DbContext dbContext, IDomainEventPublisher publisher, CancellationToken cancellationToken)
    {
        var domainEvents = dbContext.ChangeTracker
             .Entries<IAggregateRoot>()
             .Select(entityEntry => entityEntry.Entity)
             .SelectMany(entity =>
             {
                 var domainEvents = entity.DomainEvents;

                 entity.ClearDomainEvents();

                 return domainEvents;
             })
             .ToList();

        var result = await dbContext.SaveChangesAsync(cancellationToken);

        await PublishDomainEvents(domainEvents, publisher, cancellationToken);

        return result;
    }

    private static async Task PublishDomainEvents(IList<IDomainEvent> domainEvents, IDomainEventPublisher publisher, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            await publisher.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
