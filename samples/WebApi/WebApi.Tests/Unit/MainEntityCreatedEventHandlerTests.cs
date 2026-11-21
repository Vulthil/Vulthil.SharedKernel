using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;
using WebApi.Application.MainEntities.Create;
using WebApi.Domain.MainEntities;
using WebApi.Domain.MainEntities.Events;

namespace WebApi.Tests.Unit;

public sealed class MainEntityCreatedEventHandlerTests : BaseUnitTestCase<MainEntityCreatedEventHandler>
{
    [Fact]
    public async Task HandleAsync_publishes_the_integration_event()
    {
        // Arrange
        var id = new MainEntityId(Guid.NewGuid());

        // Act
        await Target.HandleAsync(new MainEntityCreatedEvent(id), CancellationToken);

        // Assert
        GetMock<IPublisher>().Verify(publisher => publisher.PublishAsync(
            It.Is<MainEntityCreatedIntegrationEvent>(integrationEvent => integrationEvent.Id == id.Value),
            CancellationToken), Times.Once);
    }
}
