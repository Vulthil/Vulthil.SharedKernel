using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;
using Shouldly;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Orders.Tests.Unit;

public sealed class OrderTests : BaseUnitTestCase
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_trims_the_description_and_raises_OrderPlacedDomainEvent()
    {
        // Act
        var result = Order.Place("  Espresso machine ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe("Espresso machine");
        result.Value.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderPlacedDomainEvent>();
    }

    [Fact]
    public void Place_with_a_too_long_description_fails()
    {
        // Act
        var result = Order.Place(new string('x', Order.MaxDescriptionLength + 1));

        // Assert
        result.Error.ShouldBe(OrderErrors.DescriptionTooLong);
    }

    [Fact]
    public void ChangeDescription_to_the_same_description_fails_without_an_event()
    {
        // Arrange
        var order = Order.Place("Espresso machine").Value;
        order.ClearDomainEvents();

        // Act
        var result = order.ChangeDescription("Espresso machine");

        // Assert
        result.Error.ShouldBe(OrderErrors.DescriptionUnchanged);
        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkShipped_twice_throws_a_domain_exception_with_a_conflict_error()
    {
        // Arrange
        var order = Order.Place("Espresso machine").Value;
        order.MarkShipped(Now);

        // Act
        var exception = Should.Throw<OrderAlreadyShippedException>(() => order.MarkShipped(Now.AddMinutes(1)));

        // Assert
        exception.Error.Type.ShouldBe(ErrorType.Conflict);
        order.ShippedAtUtc.ShouldBe(Now);
    }
}
