using System.Net;
using Orders.Endpoints;
using Orders.ExternalServices;
using Orders.Tests.Fixtures;
using Shouldly;
using Vulthil.Extensions.Testing;

namespace Orders.Tests;

/// <summary>
/// The endpoints call real HTTP clients; only the wire is mocked, so the client code, the URL and the JSON run as in
/// production.
/// </summary>
public sealed class ExternalServicesTests(OrdersWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : OrdersTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task GetShippingQuote_returns_the_quote_of_the_typed_client()
    {
        // Arrange
        HttpMock<IShippingRatesClient>()
            .On(HttpMethod.Get, "/quotes/DK")
            .RespondWith(HttpStatusCode.OK, new ShippingQuote("DK", 49.5m, 2))
            .WithHeader("X-Source", "mock");

        // Act
        var response = await Client.GetAsync("api/shipping-quotes/DK", CancellationToken);

        // Assert
        var quote = await response.GetResponseAsync<ShippingQuote>(CancellationToken);
        quote.Price.ShouldBe(49.5m);
        quote.TransitDays.ShouldBe(2);
        HttpMock<IShippingRatesClient>().ReceivedRequests.ShouldContain(request => request.RequestUri!.AbsolutePath == "/quotes/DK");
    }

    [Fact]
    public async Task GetShippingQuote_answers_a_rates_service_error_with_a_problem_response()
    {
        // Arrange
        HttpMock<IShippingRatesClient>()
            .On(HttpMethod.Get, "/quotes/SE")
            .RespondWithJson(HttpStatusCode.BadRequest, "{}");

        // Act
        using var response = await Client.GetAsync("api/shipping-quotes/SE", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetStock_replays_a_captured_json_response_of_the_named_client()
    {
        // Arrange
        const string capturedJson = """{ "sku": "CUP-0001", "available": 7 }""";
        HttpMock(GetStock.InventoryClientName)
            .On(HttpMethod.Get, "/stock/CUP-0001")
            .RespondWithJson(HttpStatusCode.OK, capturedJson);

        // Act
        var response = await Client.GetAsync("api/stock/CUP-0001", CancellationToken);

        // Assert
        var stock = await response.GetResponseAsync<GetStock.StockLevel>(CancellationToken);
        stock.Available.ShouldBe(7);
    }

    [Fact]
    public async Task GetStock_answers_an_unknown_sku_with_not_found()
    {
        // Arrange
        HttpMock(GetStock.InventoryClientName)
            .On(HttpMethod.Get, "/stock/NOPE-0000")
            .RespondWithJson(HttpStatusCode.NotFound, "{}");

        // Act
        using var response = await Client.GetAsync("api/stock/NOPE-0000", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
