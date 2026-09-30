using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class QueueDispatchPlansTests : BaseUnitTestCase
{
    private readonly Lazy<QueueDispatchPlans> _lazyTarget;

    private QueueDispatchPlans Target => _lazyTarget.Value;

    public QueueDispatchPlansTests()
    {
        _lazyTarget = new Lazy<QueueDispatchPlans>(CreateInstance<QueueDispatchPlans>);
        Use(TestProviders.Build());

        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);
        Use<IEnumerable<IConsumeFilter<TestRequest>>>([]);
    }

    private static BasicDeliverEventArgs CreateDeliverEventArgs()
        => new(
            "consumer-tag",
            1,
            false,
            "test-exchange",
            "#",
            new BasicProperties { Headers = new Dictionary<string, object?>() },
            ReadOnlyMemory<byte>.Empty);

    private static QueueDefinition QueueConsuming<TConsumer, TMessage>()
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
    {
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TConsumer)),
            MessageType = new MessageType(typeof(TMessage)),
        });
        return queue;
    }

    private static ConsumerRegistration Registration<TConsumer, TMessage>()
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
        => new()
        {
            ConsumerType = new ConsumerType(typeof(TConsumer)),
            MessageType = new MessageType(typeof(TMessage)),
        };

    private static RequestConsumerRegistration RequestRegistration<TConsumer>()
        where TConsumer : class, IRequestConsumer<TestRequest, TestResponse>
        => new()
        {
            ConsumerType = new ConsumerType(typeof(TConsumer)),
            MessageType = new MessageType(typeof(TestRequest)),
            ResponseType = typeof(TestResponse),
        };

    #region Test Messages and Consumers

    internal sealed record TestMessage(string Content);
    internal sealed record TestRequest(string Query);
    internal sealed record TestResponse(string Result);
    internal sealed record OtherMessage(string Content);

    internal interface IOrderEvent;

    internal interface IOrder : IOrderEvent;

    internal sealed record OrderPlaced(string OrderId) : IOrder;

    private sealed class TestMessageConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class OrderPlacedConsumer : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IMessageContext<OrderPlaced> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class OrderConsumer : IConsumer<IOrder>
    {
        public Task ConsumeAsync(IMessageContext<IOrder> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class OrderEventConsumer : IConsumer<IOrderEvent>
    {
        public Task ConsumeAsync(IMessageContext<IOrderEvent> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class OtherMessageConsumer : IConsumer<OtherMessage>
    {
        public Task ConsumeAsync(IMessageContext<OtherMessage> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("failed to process request");
        }
    }

    private sealed class TestRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new TestResponse($"Processed: {messageContext.Message.Query}"));
    }

    #endregion

    [Fact]
    public void PlansIncludeTheQueuesStandardConsumers()
    {
        // Arrange
        Use(QueueConsuming<TestMessageConsumer, TestMessage>());

        // Act
        var plan = Target.GetPlan(new MessageType(typeof(TestMessage)).Name);

        // Assert
        plan.ShouldNotBeNull();
        plan.Handlers.ShouldHaveSingleItem();
        plan.Handlers[0].Kind.ShouldBe(HandlerKind.Consumer);
    }

    [Fact]
    public void PlansIncludeTheQueuesRequestConsumers()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(RequestRegistration<TestRequestConsumer>());
        Use(queue);

        // Act
        var plan = Target.GetPlan(new MessageType(typeof(TestRequest)).Name);

        // Assert
        plan.ShouldNotBeNull();
        plan.Handlers.ShouldContain(h => h.Kind == HandlerKind.RequestConsumer);
    }

    [Fact]
    public void GetPlanByUrnResolvesTheSamePlanAsTheFullNameLookup()
    {
        // Arrange
        var provider = TestProviders.Build();
        Use(provider);
        Use(QueueConsuming<TestMessageConsumer, TestMessage>());

        // Act
        var byUrn = Target.GetPlanByUrn(provider.GetUrn(typeof(TestMessage)));
        var byFullName = Target.GetPlan(typeof(TestMessage).FullName!);

        // Assert
        byUrn.ShouldNotBeNull();
        byUrn.ShouldBeSameAs(byFullName);
        byUrn.MessageType.Type.ShouldBe(typeof(TestMessage));
    }

    [Fact]
    public void GetPlanShouldReturnNullForUnregisteredMessageType()
    {
        // Arrange
        Use(QueueConsuming<TestMessageConsumer, TestMessage>());

        // Act
        var plan = Target.GetPlan("NonExistentMessage");

        // Assert
        plan.ShouldBeNull();
    }

    [Fact]
    public void PlansDedupeIdenticalRegistrationsIntoOneHandler()
    {
        // Arrange
        var consumer = new ConsumerType(typeof(TestMessageConsumer));
        var messageType = new MessageType(typeof(TestMessage));

        var registration1 = new ConsumerRegistration { ConsumerType = consumer, MessageType = messageType };
        var registration2 = new ConsumerRegistration { ConsumerType = consumer, MessageType = messageType };

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration1);
        queue.AddConsumer(registration2);
        Use(queue);

        // Act
        var plan = Target.GetPlan(messageType.Name);

        // Assert
        plan.ShouldNotBeNull();
        plan.Handlers.ShouldHaveSingleItem();
        plan.Handlers[0].Kind.ShouldBe(HandlerKind.Consumer);
    }

    [Fact]
    public void ConstructionRejectsASecondRequestConsumerForTheSameMessageType()
    {
        // Arrange
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(RequestRegistration<TestRequestConsumer>());
        queue.AddConsumer(RequestRegistration<ThrowingRequestConsumer>());
        Use(queue);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => _ = Target);
        ex.Message.ShouldContain("request consumer");
        ex.Message.ShouldContain("TestQueue");
    }

    [Fact]
    public void IsPartitionedIsFalseWhenNoConsumedTypeIsPartitioned()
    {
        // Arrange
        Use(QueueConsuming<TestMessageConsumer, TestMessage>());

        // Act & Assert
        Target.IsPartitioned.ShouldBeFalse();
        Target.GetPlan(typeof(TestMessage).FullName!)!.Partition.ShouldBeNull();
    }

    [Fact]
    public void IsPartitionedIsTrueWhenAConsumedTypeIsPartitioned()
    {
        // Arrange
        Use(TestProviders.Build(messaging => messaging.UsePartitioner<TestMessage>(2, context => context.Message.Content)));
        Use(QueueConsuming<TestMessageConsumer, TestMessage>());

        // Act & Assert
        Target.IsPartitioned.ShouldBeTrue();
    }

    [Fact]
    public void PartitionedPlanExtractsTheKeyFromTheDeliveredMessage()
    {
        // Arrange
        Use(TestProviders.Build(messaging => messaging.UsePartitioner<TestMessage>(2, context => context.Message.Content)));
        var queue = QueueConsuming<TestMessageConsumer, TestMessage>();
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OtherMessageConsumer)),
            MessageType = new MessageType(typeof(OtherMessage)),
        });
        Use(queue);

        // Act
        var partition = Target.GetPlan(typeof(TestMessage).FullName!)!.Partition;
        var unpartitioned = Target.GetPlan(typeof(OtherMessage).FullName!)!.Partition;

        // Assert
        partition.ShouldNotBeNull();
        partition.Partitioner.PartitionCount.ShouldBe(2);
        partition.ExtractKey(new TestMessage("customer-42"), CreateDeliverEventArgs(), null).ShouldBe("customer-42");
        unpartitioned.ShouldBeNull();
    }

    [Fact]
    public void PlansForOneQueueDoNotSeeAnotherQueuesHandlers()
    {
        // Arrange
        var provider = TestProviders.Build(messaging =>
        {
            messaging.ConfigureQueue("alpha", queue => queue.AddConsumer<TestMessageConsumer>());
            messaging.ConfigureQueue("beta", queue =>
            {
                queue.AddConsumer<TestMessageConsumer>();
                queue.AddConsumer<OtherMessageConsumer>();
            });
        });
        var sharedUrn = provider.GetUrn(typeof(TestMessage));
        var betaOnlyUrn = provider.GetUrn(typeof(OtherMessage));

        // Act
        var alpha = new QueueDispatchPlans(provider, provider.QueueDefinitions.Single(queue => queue.Name == "alpha"));
        var beta = new QueueDispatchPlans(provider, provider.QueueDefinitions.Single(queue => queue.Name == "beta"));

        // Assert
        alpha.GetPlanByUrn(sharedUrn)!.Handlers.ShouldHaveSingleItem();
        beta.GetPlanByUrn(sharedUrn)!.Handlers.ShouldHaveSingleItem();
        alpha.GetPlanByUrn(betaOnlyUrn).ShouldBeNull();
        beta.GetPlanByUrn(betaOnlyUrn).ShouldNotBeNull();
    }

    [Fact]
    public void AConcreteMessagesPlanHoldsEveryConsumerRegisteredForATypeItIsAssignableTo()
    {
        // Arrange
        var queue = new QueueDefinition("orders");
        queue.AddSubscription(new Subscription(new MessageType(typeof(OrderPlaced))));
        queue.AddConsumer(Registration<OrderPlacedConsumer, OrderPlaced>());
        queue.AddConsumer(Registration<OrderConsumer, IOrder>());
        queue.AddConsumer(Registration<OrderEventConsumer, IOrderEvent>());
        Use(queue);

        // Act
        var plan = Target.GetPlan(typeof(OrderPlaced).FullName!);

        // Assert
        plan.ShouldNotBeNull().Handlers.Select(handler => handler.Identity).ShouldBe(
        [
            $"{typeof(OrderPlacedConsumer).FullName}:{typeof(OrderPlaced).FullName}",
            $"{typeof(OrderConsumer).FullName}:{typeof(IOrder).FullName}",
            $"{typeof(OrderEventConsumer).FullName}:{typeof(IOrderEvent).FullName}",
        ], ignoreOrder: true);
    }
}
