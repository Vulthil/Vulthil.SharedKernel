using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;
using WebApi.Application.Shipping;

namespace WebApi.Tests.Unit;

public sealed class ShipmentDeliveredValidationFilterTests : BaseUnitTestCase<ShipmentDeliveredValidationFilter>
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    protected override ValueTask Initialize()
    {
        Use<TimeProvider>(_timeProvider);
        return base.Initialize();
    }

    [Fact]
    public async Task ConsumeAsync_passes_a_valid_delivery_to_the_consumer()
    {
        // Arrange
        var context = ContextFor(new ShipmentDelivered(Guid.NewGuid(), Guid.NewGuid(), "acme-express", _timeProvider.GetUtcNow(), 3));
        var consumed = false;

        // Act
        await Target.ConsumeAsync(context, _ =>
        {
            consumed = true;
            return Task.CompletedTask;
        });

        // Assert
        consumed.ShouldBeTrue();
    }

    [Fact]
    public async Task ConsumeAsync_skips_a_delivery_dated_in_the_future()
    {
        // Arrange
        var context = ContextFor(new ShipmentDelivered(Guid.NewGuid(), Guid.NewGuid(), "acme-express", _timeProvider.GetUtcNow().AddDays(1), 3));
        var consumed = false;

        // Act
        await Target.ConsumeAsync(context, _ =>
        {
            consumed = true;
            return Task.CompletedTask;
        });

        // Assert
        consumed.ShouldBeFalse();
    }

    private static IMessageContext<ShipmentDelivered> ContextFor(ShipmentDelivered message)
    {
        var context = new Mock<IMessageContext<ShipmentDelivered>>();
        context.SetupGet(c => c.Message).Returns(message);
        return context.Object;
    }
}
