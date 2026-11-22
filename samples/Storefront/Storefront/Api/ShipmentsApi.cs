using Vulthil.Results;

namespace Storefront.Api;

public sealed class ShipmentsApi(HttpClient httpClient)
{
    public Task<Result<IReadOnlyList<ShipmentView>>> GetShipmentsAsync(Guid orderId, CancellationToken cancellationToken) =>
        ApiResponse.ReadAsync<IReadOnlyList<ShipmentView>>(() => httpClient.GetAsync($"shipments?orderReference={orderId}", cancellationToken), cancellationToken);
}
