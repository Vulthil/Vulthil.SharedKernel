using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Orders.Application.Orders.Place;
using Orders.Application.Fulfillments;
using Orders.Application.Fulfillments.GetInProgress;
using Orders.Domain.Fulfillments;
using Orders.Tests.Fixtures;

namespace Orders.Tests;

public sealed class FulfillmentIntegrationTests(OrdersWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : OrdersTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task Placing_an_order_starts_its_fulfillment()
    {
        // Arrange
        var command = new PlaceOrderCommand(Guid.NewGuid().ToString());
        var placed = await Sender.SendAsync(command, CancellationToken);
        placed.IsSuccess.ShouldBeTrue();

        var query = new GetInProgressFulfillmentsQuery();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var queryResult = await Sender.SendAsync(query, CancellationToken);

            if (queryResult.IsFailure)
            {
                return Result.Failure<List<FulfillmentDto>>(Error.NullValue);
            }

            if (!queryResult.Value.Any(s => s.OrderId == placed.Value))
            {
                return Result.Failure<List<FulfillmentDto>>(Error.NotFound("Fulfillment.NotFound", "The fulfillment does not exist yet."));
            }

            return queryResult;
        }, cancellationToken: CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s =>
            s.OrderId == placed.Value && s.Status is FulfillmentStatus.InProgressStatus);
    }

    [Fact]
    public async Task Fulfillment_starts_at_the_time_of_a_TimeProvider_replaced_for_this_test()
    {
        // Arrange
        var now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Use<TimeProvider>(new FrozenClock(now));
        var placed = await Sender.SendAsync(new PlaceOrderCommand(Guid.NewGuid().ToString()), CancellationToken);

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var queryResult = await Sender.SendAsync(new GetInProgressFulfillmentsQuery(), CancellationToken);
            return queryResult.Bind(fulfillments => fulfillments.Find(s => s.OrderId == placed.Value)
                .ToResult(Error.NotFound("Fulfillment.NotFound", "The fulfillment does not exist yet.")));
        }, cancellationToken: CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBeOfType<FulfillmentStatus.InProgressStatus>().ProgressTime.ShouldBe(now);
    }

    /// <summary>
    /// Stops the wall clock but keeps real timers, so the outbox relay and the consumers still run on schedule.
    /// </summary>
    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
