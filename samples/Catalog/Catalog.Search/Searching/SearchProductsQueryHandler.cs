using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Search.Searching;

/// <summary>
/// The validation pipeline behavior validates commands only, so this query guards its input on the railway itself:
/// <c>Ensure</c> turns a short term into a validation failure, which the API answers with a 400.
/// </summary>
internal sealed class SearchProductsQueryHandler(ISearchIndexStore store)
    : IQueryHandler<SearchProductsQuery, Result<IReadOnlyList<ProductSearchResult>>>
{
    public Task<Result<IReadOnlyList<ProductSearchResult>>> HandleAsync(SearchProductsQuery request, CancellationToken cancellationToken = default) =>
        Result.Success(ProductSearchEntry.Normalize(request.Term ?? string.Empty))
            .Ensure(term => term.Length >= SearchErrors.MinimumTermLength, SearchErrors.TermTooShort)
            .BindAsync(term => SearchAsync(term, cancellationToken));

    private async Task<Result<IReadOnlyList<ProductSearchResult>>> SearchAsync(string normalizedTerm, CancellationToken cancellationToken)
    {
        var matches = await store.SearchEntries
            .AsNoTracking()
            .Where(entry => entry.NormalizedName.Contains(normalizedTerm))
            .OrderBy(entry => entry.Name)
            .Take(50)
            .Select(entry => new ProductSearchResult(entry.ProductId, entry.Sku, entry.Name, entry.Price))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductSearchResult>>(matches);
    }
}
