using System.Net.Http.Json;

namespace Orders.ExternalServices;

/// <summary>
/// Client for an external shipping-rates API, registered as a typed <see cref="HttpClient"/>. The tests replace its
/// wire with an HTTP mock; the sample has no real rates server.
/// </summary>
public interface IShippingRatesClient
{
    Task<ShippingQuote?> GetQuoteAsync(string country, CancellationToken cancellationToken);
}

internal sealed class ShippingRatesClient(HttpClient httpClient) : IShippingRatesClient
{
    public Task<ShippingQuote?> GetQuoteAsync(string country, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<ShippingQuote>($"/quotes/{Uri.EscapeDataString(country)}", cancellationToken);
}

public sealed record ShippingQuote(string Country, decimal Price, int TransitDays);
