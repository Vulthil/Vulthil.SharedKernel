using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Application;

/// <summary>
/// A plain command, not an <see cref="ITransactionalCommand"/>: Cosmos DB has no transaction to open. The domain event
/// still reaches the outbox in the same save as the product.
/// </summary>
public sealed record AddProductCommand(string Sku, string Name, decimal Price) : ICommand<Result<Guid>>;
