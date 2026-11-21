using System.Net.Http.Json;
using Vulthil.Results;

namespace Storefront.Api;

public sealed class CatalogApi(HttpClient httpClient)
{
    public Task<Result<IReadOnlyList<ProductView>>> SearchAsync(string term, CancellationToken cancellationToken) =>
        ApiResponse.ReadAsync<IReadOnlyList<ProductView>>(
            () => httpClient.GetAsync($"search/products?term={Uri.EscapeDataString(term)}", cancellationToken),
            cancellationToken);

    public async Task<Result<Guid>> AddProductAsync(string sku, string name, decimal price, CancellationToken cancellationToken) =>
        (await ApiResponse.ReadAsync<CreatedId>(() => httpClient.PostAsJsonAsync("products", new { sku, name, price }, cancellationToken), cancellationToken))
            .Map(created => created.Id);
}
