using Catalog.Domain.Events;
using Catalog.Infrastructure;
using Catalog.Search.Contracts;
using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.SharedKernel.Events;

namespace Catalog.Application;

/// <summary>
/// Runs when the outbox relay dispatches <see cref="ProductAddedDomainEvent"/>. Cosmos DB has no transaction for the
/// transactional bus outbox to join, so the event is published straight to the broker; the relay retries a failed
/// publish, and the fixed message id lets the consumer's inbox drop the copies a retry sends.
/// </summary>
public sealed class ProductAddedDomainEventHandler(CatalogDbContext dbContext, IPublisher publisher) : IDomainEventHandler<ProductAddedDomainEvent>
{
    public async Task HandleAsync(ProductAddedDomainEvent notification, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == notification.ProductId, cancellationToken);

        await publisher.PublishAsync(
            new ProductPublished(product.Id.Value, product.Sku, product.Name, product.Price),
            context =>
            {
                context.SetMessageId(ProductPublished.MessageIdFor(product.Id.Value));
                context.SetCorrelationId(product.Sku);
                return ValueTask.CompletedTask;
            },
            cancellationToken);
    }
}
