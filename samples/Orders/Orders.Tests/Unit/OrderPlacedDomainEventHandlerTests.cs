using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;
using Orders.Application.Orders.Place;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;

namespace Orders.Tests.Unit;

public sealed class OrderPlacedDomainEventHandlerTests : BaseUnitTestCase<OrderPlacedDomainEventHandler>
{
    [Fact]
    public async Task HandleAsync_publishes_the_integration_event()
    {
        // Arrange
        var id = new OrderId(Guid.NewGuid());

        // Act
        await Target.HandleAsync(new OrderPlacedDomainEvent(id), CancellationToken);

        // Assert
        GetMock<IPublisher>().Verify(publisher => publisher.PublishAsync(
            It.Is<OrderPlacedIntegrationEvent>(integrationEvent => integrationEvent.OrderId == id.Value),
            CancellationToken), Times.Once);
    }
}
