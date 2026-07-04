using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class PolymorphicDispatchTests : BaseUnitTestCase
{
    private readonly Lazy<MessageTypeCache> _lazyTarget;
    private MessageTypeCache Target => _lazyTarget.Value;

    public PolymorphicDispatchTests()
    {
        Use(TestProviders.Build());
        Use<IEnumerable<IConsumeFilter<OrderPlaced>>>([]);
        Use<IEnumerable<IConsumeFilter<IOrder>>>([]);
        Use<IEnumerable<IConsumeFilter<IOrderEvent>>>([]);
        _lazyTarget = new Lazy<MessageTypeCache>(CreateInstance<MessageTypeCache>);
    }

    [Fact]
    public async Task ConcreteDeliveryFiresEveryAssignableHandler()
    {
        // Arrange
        var concreteHits = 0;
        var orderInterfaceHits = 0;
        var orderEventInterfaceHits = 0;

        Use(new ConcreteConsumer(() => concreteHits++));
        Use(new OrderInterfaceConsumer(() => orderInterfaceHits++));
        Use(new OrderEventInterfaceConsumer(() => orderEventInterfaceHits++));

        var queue = new QueueDefinition("orders");
        queue.AddSubscription(new Subscription(new MessageType(typeof(OrderPlaced))));
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(ConcreteConsumer)),
            MessageType = new MessageType(typeof(OrderPlaced)),
        });
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderInterfaceConsumer)),
            MessageType = new MessageType(typeof(IOrder)),
        });
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderEventInterfaceConsumer)),
            MessageType = new MessageType(typeof(IOrderEvent)),
        });
        Target.RegisterQueue(queue);

        var plan = Target.GetPlan(typeof(OrderPlaced).FullName!);
        plan.ShouldNotBeNull();
        plan.Handlers.Count.ShouldBe(3);

        var message = new OrderPlaced("abc");
        var ea = CreateDeliverEventArgs();

        // Act
        foreach (var handler in plan.Handlers)
        {
            await handler.DispatchAsync(AutoMocker, message, ea, (MessageEnvelope?)null, new RecordingGatedPublisher().PublishAsync, CancellationToken.None);
        }

        // Assert
        concreteHits.ShouldBe(1);
        orderInterfaceHits.ShouldBe(1);
        orderEventInterfaceHits.ShouldBe(1);
    }

    private static RabbitMQ.Client.Events.BasicDeliverEventArgs CreateDeliverEventArgs()
        => new(
            consumerTag: "tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            properties: new BasicProperties { Headers = new Dictionary<string, object?>() },
            body: ReadOnlyMemory<byte>.Empty);
}

internal interface IOrderEvent { }
internal interface IOrder : IOrderEvent { }
internal sealed record OrderPlaced(string OrderId) : IOrder;

internal sealed class ConcreteConsumer(Action onConsume) : IConsumer<OrderPlaced>
{
    public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
    {
        onConsume();
        return Task.CompletedTask;
    }
}

internal sealed class OrderInterfaceConsumer(Action onConsume) : IConsumer<IOrder>
{
    public Task ConsumeAsync(IMessageContext<IOrder> messageContext, CancellationToken cancellationToken = default)
    {
        onConsume();
        return Task.CompletedTask;
    }
}

internal sealed class OrderEventInterfaceConsumer(Action onConsume) : IConsumer<IOrderEvent>
{
    public Task ConsumeAsync(IMessageContext<IOrderEvent> messageContext, CancellationToken cancellationToken = default)
    {
        onConsume();
        return Task.CompletedTask;
    }
}
