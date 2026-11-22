using System.Net.Http.Json;
using Vulthil.Results;

namespace Storefront.Api;

public sealed class OrdersApi(HttpClient httpClient)
{
    public async Task<Result<IReadOnlyList<OrderView>>> GetOrdersAsync(CancellationToken cancellationToken) =>
        (await ApiResponse.ReadAsync<OrderList>(() => httpClient.GetAsync("api/orders", cancellationToken), cancellationToken))
            .Map(list => list.Orders);

    public Task<Result<OrderView>> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        ApiResponse.ReadAsync<OrderView>(() => httpClient.GetAsync($"api/orders/{orderId}", cancellationToken), cancellationToken);

    public async Task<Result<Guid>> PlaceOrderAsync(string description, CancellationToken cancellationToken) =>
        (await ApiResponse.ReadAsync<CreatedId>(() => httpClient.PostAsJsonAsync("api/orders", new { description }, cancellationToken), cancellationToken))
            .Map(created => created.Id);

    public Task<Result> ShipOrderAsync(Guid orderId, string recipient, CancellationToken cancellationToken) =>
        ApiResponse.SendAsync(() => httpClient.PostAsJsonAsync($"api/orders/{orderId}/shipments", new { recipient }, cancellationToken), cancellationToken);

    private sealed record OrderList(IReadOnlyList<OrderView> Orders);
}
