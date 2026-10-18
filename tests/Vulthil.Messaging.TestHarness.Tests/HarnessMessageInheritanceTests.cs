using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

public sealed class HarnessMessageInheritanceTests : BaseUnitTestCase
{
    private readonly IHost _host;

    private ITestHarness Harness => _host.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => _host.Services.GetRequiredService<IPublisher>();

    public HarnessMessageInheritanceTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMessaging(messaging =>
        {
            messaging.ConfigureMessage<OrderEvent>(order => order.UseCorrelationId(o => o.OrderId.ToString()));
            messaging.ConfigureQueue("order-events", queue =>
            {
                queue.Subscribe<OrderPlaced>();
                queue.Subscribe<OrderCancelled>();
                queue.AddConsumer<OrderEventConsumer>();
            });
            messaging.UseTestHarness();
        });
        _host = builder.Build();
    }

    protected override ValueTask Dispose()
    {
        _host.Dispose();
        return base.Dispose();
    }

    [Fact]
    public async Task EachTypeThatDerivesFromAConfiguredBaseIsDeliveredAsItsOwnTypeWithTheInheritedSettings()
    {
        // Arrange
        var placed = new OrderPlaced(Guid.NewGuid());
        var cancelled = new OrderCancelled(Guid.NewGuid());

        // Act
        await Publisher.PublishAsync(placed, CancellationToken);
        await Publisher.PublishAsync(cancelled, CancellationToken);

        // Assert
        var placedDelivery = Harness.Consumed<OrderPlaced>().ShouldHaveSingleItem();
        placedDelivery.Message.ShouldBe(placed);
        placedDelivery.Envelope.CorrelationId.ShouldBe(placed.OrderId.ToString());
        var cancelledDelivery = Harness.Consumed<OrderCancelled>().ShouldHaveSingleItem();
        cancelledDelivery.Message.ShouldBe(cancelled);
        cancelledDelivery.Envelope.CorrelationId.ShouldBe(cancelled.OrderId.ToString());
        cancelledDelivery.Envelope.MessageType.ShouldNotBe(placedDelivery.Envelope.MessageType);
    }

    public abstract record OrderEvent(Guid OrderId);

    public sealed record OrderPlaced(Guid OrderId) : OrderEvent(OrderId);

    public sealed record OrderCancelled(Guid OrderId) : OrderEvent(OrderId);

    public sealed class OrderEventConsumer : IConsumer<OrderEvent>
    {
        public Task ConsumeAsync(IMessageContext<OrderEvent> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
