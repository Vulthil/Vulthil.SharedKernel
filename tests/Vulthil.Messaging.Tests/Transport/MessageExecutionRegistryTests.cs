using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

/// <summary>A transport-specific handler stand-in for exercising the agnostic registry.</summary>
public sealed record FakeHandler(string Label);

/// <summary>A fake factory that labels handlers by their registration shape, so tests can assert what was built.</summary>
public sealed class FakeHandlerFactory : IMessageHandlerFactory<FakeHandler>
{
    public HandlerEntry<FakeHandler> ForConsumer(Type consumerType, Type messageType, RetryPolicyDefinition? retryPolicy)
        => new(new FakeHandler($"consumer:{consumerType.Name}:{messageType.Name}"), HandlerKind.Consumer);

    public HandlerEntry<FakeHandler> ForRequestConsumer(Type consumerType, Type requestType, Type responseType, RetryPolicyDefinition? retryPolicy)
        => new(new FakeHandler($"request:{consumerType.Name}:{requestType.Name}"), HandlerKind.RequestConsumer);
}

public sealed class MessageExecutionRegistryTests : BaseUnitTestCase<MessageExecutionRegistry<FakeHandler>>
{
    public MessageExecutionRegistryTests()
    {
        GetMock<IMessageConfigurationProvider>()
            .Setup(p => p.GetUrn(It.IsAny<Type>()))
            .Returns<Type>(t => new Uri($"urn:test:{t.FullName}"));
        Use<IMessageHandlerFactory<FakeHandler>>(new FakeHandlerFactory());
    }

    private abstract record OrderEvent(string Id);
    private sealed record OrderPlaced(string Id) : OrderEvent(Id);
    private sealed record OrderShipped(string Id) : OrderEvent(Id);
    private sealed record Ping(string Id);
    private sealed record Pong(string Id);

    private sealed class OrderConsumer : IConsumer<OrderEvent>
    {
        public Task ConsumeAsync(IMessageContext<OrderEvent> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class PingConsumer : IRequestConsumer<Ping, Pong>
    {
        public Task<Pong> ConsumeAsync(IMessageContext<Ping> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pong(messageContext.Message.Id));
    }

    private sealed class OtherPingConsumer : IRequestConsumer<Ping, Pong>
    {
        public Task<Pong> ConsumeAsync(IMessageContext<Ping> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pong(messageContext.Message.Id));
    }

    private static QueueDefinition Queue(string name = "test-queue") => new(name);

    [Fact]
    public void RegisterQueueBuildsAPlanWithTheConsumerHandler()
    {
        // Arrange
        var queue = Queue();
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderConsumer)),
            MessageType = new MessageType(typeof(OrderPlaced)),
        });

        // Act
        Target.RegisterQueue(queue);

        // Assert
        var plan = Target.GetPlan(new MessageType(typeof(OrderPlaced)).Name);
        plan.ShouldNotBeNull();
        plan.Handlers.ShouldHaveSingleItem();
        plan.IsPartitioned.ShouldBeFalse();
    }

    [Fact]
    public void RegisterQueueDedupesIdenticalRegistrationsIntoOneHandler()
    {
        // Arrange
        var queue = Queue();
        var registration = new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderConsumer)),
            MessageType = new MessageType(typeof(OrderPlaced)),
        };
        queue.AddConsumer(registration);
        queue.AddConsumer(registration);

        // Act
        Target.RegisterQueue(queue);

        // Assert
        Target.GetPlan(new MessageType(typeof(OrderPlaced)).Name)!.Handlers.ShouldHaveSingleItem();
    }

    [Fact]
    public void RegisterQueueRejectsASecondRequestConsumerForTheSameMessageType()
    {
        // Arrange
        var queue = Queue();
        queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(PingConsumer)),
            MessageType = new MessageType(typeof(Ping)),
            ResponseType = typeof(Pong),
        });
        queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OtherPingConsumer)),
            MessageType = new MessageType(typeof(Ping)),
            ResponseType = typeof(Pong),
        });

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => Target.RegisterQueue(queue));
        exception.Message.ShouldContain("request consumer");
        exception.Message.ShouldContain("test-queue");
    }

    [Fact]
    public void RegisterQueueFansAPolymorphicRegistrationOutAcrossConcreteSubscriptions()
    {
        // Arrange
        var queue = Queue();
        queue.AddSubscription(new Subscription(new MessageType(typeof(OrderPlaced))));
        queue.AddSubscription(new Subscription(new MessageType(typeof(OrderShipped))));
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderConsumer)),
            MessageType = new MessageType(typeof(OrderEvent)),
        });

        // Act
        Target.RegisterQueue(queue);

        // Assert
        Target.GetPlan(new MessageType(typeof(OrderPlaced)).Name)!.Handlers.ShouldHaveSingleItem();
        Target.GetPlan(new MessageType(typeof(OrderShipped)).Name)!.Handlers.ShouldHaveSingleItem();
    }

    [Fact]
    public void GetPlanReturnsNullForAnUnregisteredMessageType()
    {
        // Act
        var plan = Target.GetPlan("urn:test:does.not.exist");

        // Assert
        plan.ShouldBeNull();
    }

    [Fact]
    public void GetPlanByUrnResolvesTheSamePlanAsTheFullNameLookup()
    {
        // Arrange
        var queue = Queue();
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderConsumer)),
            MessageType = new MessageType(typeof(OrderPlaced)),
        });
        Target.RegisterQueue(queue);

        // Act
        var byUrn = Target.GetPlanByUrn(new Uri($"urn:test:{typeof(OrderPlaced).FullName}"));
        var byFullName = Target.GetPlanByFullName(typeof(OrderPlaced).FullName!);

        // Assert
        byUrn.ShouldNotBeNull();
        byUrn.ShouldBeSameAs(byFullName);
    }

    [Fact]
    public void RegisterQueueAttachesThePartitionSpecForPartitionedTypes()
    {
        // Arrange
        var spec = new PartitionSpec(new Partitioner(4), (Func<OrderPlaced, string>)(o => o.Id));
        GetMock<IMessageConfigurationProvider>()
            .Setup(p => p.GetPartition(typeof(OrderPlaced)))
            .Returns(spec);

        var queue = Queue();
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderConsumer)),
            MessageType = new MessageType(typeof(OrderPlaced)),
        });

        // Act
        Target.RegisterQueue(queue);

        // Assert
        var plan = Target.GetPlan(new MessageType(typeof(OrderPlaced)).Name);
        plan.ShouldNotBeNull();
        plan.Partition.ShouldBeSameAs(spec);
        plan.IsPartitioned.ShouldBeTrue();
    }
}
