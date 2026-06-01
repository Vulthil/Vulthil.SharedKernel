using System.Net.Http.Json;

namespace WebApi.ExternalServices;

/// <summary>
/// Client for an external weather API. Demonstrates an outbound HTTP dependency that integration tests mock.
/// </summary>
public interface IExternalWeatherClient
{
    /// <summary>
    /// Gets the forecast for the given city from the external weather API.
    /// </summary>
    /// <param name="city">The city to fetch the forecast for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The forecast, or <see langword="null"/> if none was returned.</returns>
    Task<ExternalForecast?> GetForecastAsync(string city, CancellationToken cancellationToken);
}

internal sealed class ExternalWeatherClient(HttpClient httpClient) : IExternalWeatherClient
{
    public Task<ExternalForecast?> GetForecastAsync(string city, CancellationToken cancellationToken)
        => httpClient.GetFromJsonAsync<ExternalForecast>($"/forecast/{city}", cancellationToken);
}

/// <summary>
/// A weather forecast returned by the external weather API.
/// </summary>
/// <param name="City">The city the forecast is for.</param>
/// <param name="TemperatureC">The temperature in degrees Celsius.</param>
public sealed record ExternalForecast(string City, int TemperatureC);
