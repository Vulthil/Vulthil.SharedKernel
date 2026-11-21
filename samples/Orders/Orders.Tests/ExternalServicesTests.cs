using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebApi.ExternalServices;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class ExternalWeatherClientTests(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(factory, testOutputHelper)
{
    private IExternalWeatherClient WeatherClient => ScopedServices.GetRequiredService<IExternalWeatherClient>();

    [Fact]
    public async Task GetForecast_returns_strongly_typed_mocked_response()
    {
        // Arrange
        HttpMock<IExternalWeatherClient>()
            .On(HttpMethod.Get, "/forecast/london")
            .RespondWith(HttpStatusCode.OK, new ExternalForecast("London", 18))
            .WithHeader("X-Source", "mock");

        // Act
        var forecast = await WeatherClient.GetForecastAsync("london", CancellationToken);

        // Assert
        forecast.ShouldNotBeNull();
        forecast.City.ShouldBe("London");
        forecast.TemperatureC.ShouldBe(18);
    }

    [Fact]
    public async Task GetForecast_returns_captured_json_string()
    {
        // Arrange
        const string capturedJson = """{ "city": "Paris", "temperatureC": 21 }""";
        HttpMock<IExternalWeatherClient>()
            .On(HttpMethod.Get, "/forecast/paris")
            .RespondWithJson(HttpStatusCode.OK, capturedJson);

        // Act
        var forecast = await WeatherClient.GetForecastAsync("paris", CancellationToken);

        // Assert
        forecast.ShouldNotBeNull();
        forecast.City.ShouldBe("Paris");
        forecast.TemperatureC.ShouldBe(21);
        HttpMock<IExternalWeatherClient>().ReceivedRequests
            .ShouldContain(request => request.RequestUri!.AbsolutePath == "/forecast/paris");
    }

    [Fact]
    public async Task Named_client_returns_mocked_response()
    {
        // Arrange
        HttpMock("inventory")
            .On(HttpMethod.Get, "/stock/42")
            .RespondWithJson(HttpStatusCode.OK, """{ "available": 7 }""");

        var inventoryClient = ScopedServices.GetRequiredService<IHttpClientFactory>().CreateClient("inventory");

        // Act
        var response = await inventoryClient.GetAsync("/stock/42", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        body.ShouldContain("\"available\": 7");
    }
}
