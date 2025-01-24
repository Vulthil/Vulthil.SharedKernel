using MediatR;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.Data;

public static class DbContextExtensions
{
    public static async Task<int> SaveAndPublishChangesAsync(this DbContext dbContext, IPublisher publisher, CancellationToken cancellationToken)
    {
        var result = await dbContext.SaveChangesAsync(cancellationToken);

        await PublishDomainEvents(dbContext, publisher, cancellationToken);

        return result;
    }

    private static async Task PublishDomainEvents(DbContext dbContext, IPublisher publisher, CancellationToken cancellationToken)
    {
        var domainEvents = dbContext.ChangeTracker
             .Entries<IEntity>()
             .Select(entityEntry => entityEntry.Entity)
             .SelectMany(entity =>
             {
                 var domainEvents = entity.DomainEvents;

                 entity.ClearDomainEvents();

                 return domainEvents;
             })
             .ToList();

        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }
    }

}
