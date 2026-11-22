using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Orders.ChangeDescription;
using Orders.Application.Orders.GetById;
using Orders.Application.Orders.Place;
using Orders.Domain.Orders.Events;
using Orders.Endpoints;
using Orders.Infrastructure.Data;
using Orders.Tests.Fixtures;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Vulthil.SharedKernel.Outbox;

namespace Orders.Tests;

public sealed class OrderIntegrationTests(OrdersWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : OrdersTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task PlaceOrder_Endpoint_returns_the_new_order_id()
    {
        // Act
        var response = await Client.PostAsJsonAsync("api/orders", new PlaceOrder.Request("Espresso machine"), CancellationToken);

        // Assert
        var result = await response.GetResponseAsync<PlaceOrder.Response>(CancellationToken);
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task PlaceOrder_Endpoint_rejects_an_empty_description_with_a_validation_problem_keyed_by_the_field()
    {
        // Act
        using var response = await Client.PostAsJsonAsync("api/orders", new PlaceOrder.Request(string.Empty), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errors"]!["Description"]!.AsArray().Select(message => (string?)message).ShouldBe(["'Description' must not be empty."]);
        problem["errorCodes"]!["Description"]!.AsArray().Select(code => (string?)code).ShouldBe(["NotEmptyValidator"]);
    }

    [Fact]
    public async Task GetOrderQuery_returns_the_placed_order()
    {
        // Arrange
        var command = new PlaceOrderCommand(Guid.NewGuid().ToString());
        var placed = await Sender.SendAsync(command, CancellationToken);

        // Act
        var order = await Sender.SendAsync(new GetOrderQuery(placed.Value), CancellationToken);

        // Assert
        order.IsSuccess.ShouldBeTrue();
        order.Value.Description.ShouldBe(command.Description);
        order.Value.ShippedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task ChangeOrderDescription_relays_its_domain_event_through_the_outbox()
    {
        // Arrange
        var orderId = await PlaceAsync();

        // Act
        var changed = await Sender.SendAsync(new ChangeOrderDescriptionCommand(orderId, Guid.NewGuid().ToString()), CancellationToken);

        // Assert
        changed.IsSuccess.ShouldBeTrue();
        var json = $$"""{"OrderId": { "Value": "{{orderId}}" } }""";
        var relayed = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var dbContext = ScopedServices.GetRequiredService<OrdersDbContext>();
            var outboxMessage = await dbContext.OutboxMessages
                .Where(o => EF.Functions.JsonContains(o.Content, json)
                    && o.ProcessedOnUtc.HasValue
                    && o.Type == typeof(OrderDescriptionChangedDomainEvent).FullName)
                .FirstOrDefaultAsync();

            return outboxMessage.ToResult(Error.NotFound("OutboxMessage.NotFound", "The domain event is not relayed yet."));
        }, cancellationToken: CancellationToken);

        relayed.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeOrderDescription_Endpoint_returns_no_content()
    {
        // Arrange
        var orderId = await PlaceAsync();

        // Act
        using var response = await Client.PutAsJsonAsync($"api/orders/{orderId}", new ChangeOrderDescription.Request("Espresso machine, red"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangeOrderDescription_Endpoint_lists_the_domain_error_code_of_a_too_long_description()
    {
        // Arrange
        var orderId = await PlaceAsync();

        // Act
        using var response = await Client.PutAsJsonAsync($"api/orders/{orderId}", new ChangeOrderDescription.Request(new string('x', 101)), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errorCodes"]!["Description"]!.AsArray().Select(code => (string?)code).ShouldBe(["Order.DescriptionTooLong"]);
    }

    [Fact]
    public async Task ChangeOrderDescription_Endpoint_returns_a_conflict_for_an_unchanged_description()
    {
        // Arrange
        var description = Guid.NewGuid().ToString();
        var placed = await Sender.SendAsync(new PlaceOrderCommand(description), CancellationToken);

        // Act
        using var response = await Client.PutAsJsonAsync($"api/orders/{placed.Value}", new ChangeOrderDescription.Request(description), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangeOrderDescription_Endpoint_returns_not_found_for_an_unknown_order()
    {
        // Act
        using var response = await Client.PutAsJsonAsync($"api/orders/{Guid.NewGuid()}", new ChangeOrderDescription.Request("Anything"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlaceOrders_Endpoint_places_every_order()
    {
        // Act
        using var response = await Client.PostAsJsonAsync("api/orders/batch", new PlaceOrders.Request(["Cup", "Saucer", "Spoon"]), CancellationToken);

        // Assert
        var result = await response.GetResponseAsync<PlaceOrders.Response>(CancellationToken);
        result.Ids.Count.ShouldBe(3);
        result.Ids.ShouldBeUnique();
    }

    [Fact]
    public async Task PlaceOrders_Endpoint_places_none_when_one_description_is_invalid()
    {
        // Arrange
        var marker = Guid.NewGuid().ToString();

        // Act
        using var response = await Client.PostAsJsonAsync("api/orders/batch", new PlaceOrders.Request([marker, " "]), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var dbContext = ScopedServices.GetRequiredService<OrdersDbContext>();
        (await dbContext.Orders.AnyAsync(order => order.Description == marker, CancellationToken)).ShouldBeFalse();
    }

    private async Task<Guid> PlaceAsync()
    {
        var result = await Sender.SendAsync(new PlaceOrderCommand(Guid.NewGuid().ToString()), CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
