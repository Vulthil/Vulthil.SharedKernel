using Catalog.Search.Contracts;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Search.Indexing;

/// <summary>
/// Turns the integration event into an application command, so indexing runs through the same pipeline behaviors
/// (logging, validation) as a request from the API.
/// </summary>
public sealed class ProductPublishedConsumer(ISender sender, ILogger<ProductPublishedConsumer> logger) : IConsumer<ProductPublished>
{
    public async Task ConsumeAsync(IMessageContext<ProductPublished> messageContext, CancellationToken cancellationToken = default)
    {
        var product = messageContext.Message;
        var result = await sender.SendAsync(new IndexProductCommand(product.ProductId, product.Sku, product.Name, product.Price), cancellationToken);

        // A product that fails validation fails it on every retry, so it is logged and acknowledged instead of thrown.
        result.TapError(error => logger.LogWarning("Product {ProductId} was not indexed: {Error}", product.ProductId, error.Description));
    }
}
