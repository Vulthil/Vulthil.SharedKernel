using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Search.Searching;

public sealed record SearchProductsQuery(string Term) : IQuery<Result<IReadOnlyList<ProductSearchResult>>>;
