using System.Net;
using System.Net.Http.Json;
using Orders.ExternalServices;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;

namespace Orders.Endpoints;

public static class GetStock
{
    public const string InventoryClientName = "inventory";

    public sealed record StockLevel(string Sku, int Available);

    /// <summary>
    /// Calls the external inventory API through a named <see cref="HttpClient"/>. A 404 from the inventory becomes a
    /// not-found result, and any other failure an unavailable one.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("stock/{sku}", async (string sku, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
                {
                    var result = await GetStockAsync(httpClientFactory.CreateClient(InventoryClientName), sku, cancellationToken);
                    return result.ToIResult();
                })
                .WithName("GetStock")
                .ProducesErrors(ErrorType.NotFound, ErrorType.Failure);

        private static async Task<Result<StockLevel>> GetStockAsync(HttpClient inventory, string sku, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await inventory.GetAsync($"/stock/{Uri.EscapeDataString(sku)}", cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return Result.Failure<StockLevel>(ExternalServiceErrors.UnknownSku(sku));
                }

                response.EnsureSuccessStatusCode();
                var stock = await response.Content.ReadFromJsonAsync<StockLevel>(cancellationToken);
                return stock.ToResult(ExternalServiceErrors.Unavailable(InventoryClientName));
            }
            catch (HttpRequestException)
            {
                return Result.Failure<StockLevel>(ExternalServiceErrors.Unavailable(InventoryClientName));
            }
        }
    }
}
