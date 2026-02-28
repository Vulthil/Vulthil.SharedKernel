using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// Extension methods for <see cref="DbContext"/> to support domain event dispatching on save.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Saves changes and publishes domain events raised by tracked aggregate roots.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="publisher">The domain event publisher.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written.</returns>
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
