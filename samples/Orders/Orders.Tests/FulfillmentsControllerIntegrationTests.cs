using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Orders.Application.Orders.Place;
using Orders.Application.Fulfillments;
using Orders.Domain.Fulfillments;
using Orders.Tests.Fixtures;

namespace Orders.Tests;

public sealed class FulfillmentsControllerIntegrationTests(OrdersWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : OrdersTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task GetInProgress_Endpoint_lists_the_fulfillment_of_a_new_order()
    {
        // Arrange
        var command = new PlaceOrderCommand(Guid.NewGuid().ToString());
        var placed = await Sender.SendAsync(command, CancellationToken);
        placed.IsSuccess.ShouldBeTrue();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var response = await Client.GetAsync("api/Fulfillments/in-progress", CancellationToken);
            var fulfillments = await response.GetResponseAsync<List<FulfillmentDto>>(CancellationToken);

            if (!fulfillments.Exists(s => s.OrderId == placed.Value))
            {
                return Result.Failure<List<FulfillmentDto>>(Error.NotFound("Fulfillment.NotFound", "The fulfillment does not exist yet."));
            }

            return Result.Success(fulfillments);
        }, cancellationToken: CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s => s.OrderId == placed.Value && s.Status is FulfillmentStatus.InProgressStatus);
    }

    [Fact]
    public async Task GetById_Endpoint_returns_a_not_found_problem_for_an_unknown_fulfillment()
    {
        // Act
        using var response = await Client.GetAsync($"api/Fulfillments/{Guid.NewGuid()}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}
