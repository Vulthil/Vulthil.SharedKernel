using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Search.Indexing;

public sealed record IndexProductCommand(Guid ProductId, string Sku, string Name, decimal Price) : ICommand;
