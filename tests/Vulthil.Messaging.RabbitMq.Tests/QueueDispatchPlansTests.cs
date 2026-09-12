using System.Text.Json;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly RecordingGatedPublisher _publisher = new();

    private QueueDispatchPlans Target => _lazyTarget.Value;

    public QueueDispatchPlansTests()
    {
        _lazyTarget = new Lazy<QueueDispatchPlans>(CreateInstance<QueueDispatchPlans>);
        Use(TestProviders.Build());

        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);
        Use<IEnumerable<IConsumeFilter<TestRequest>>>([]);
        _serviceProvider = AutoMocker;
    }

    private static BasicDeliverEventArgs CreateDeliverEventArgs(string routingKey = "#", string? replyTo = null, string? correlationId = null)
    {
        return new BasicDeliverEventArgs(
            "consumer-tag",
            1,
            false,
            "test-exchange",
            routingKey,
            new BasicProperties
            {
                ReplyTo = replyTo,
                CorrelationId = correlationId,
                Headers = new Dictionary<string, object?>()
            },
            ReadOnlyMemory<byte>.Empty);
    }

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

    private sealed class TestMessageConsumer : IConsumer<TestMessage>
    {
        public List<TestMessage> ReceivedMessages { get; } = [];

        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            ReceivedMessages.Add(messageContext.Message);
            return Task.CompletedTask;
        }
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
        public List<TestRequest> ReceivedRequests { get; } = [];

        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
        {
            ReceivedRequests.Add(messageContext.Message);
            return Task.FromResult(new TestResponse($"Processed: {messageContext.Message.Query}"));
        }
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
    public async Task CompiledHandlerShouldCallConsumerWithCorrectMessage()
    {
        // Arrange
        var consumerInstance = new TestMessageConsumer();
        Use(consumerInstance);
        Use(QueueConsuming<TestMessageConsumer, TestMessage>());

        var plan = Target.GetPlan(new MessageType(typeof(TestMessage)).Name);
        var handler = plan!.Handlers[0];
        var testMessage = new TestMessage("Hello, World!");

        // Act
        await handler.DispatchAsync(_serviceProvider, testMessage, CreateDeliverEventArgs(), null, _publisher.PublishAsync, CancellationToken.None);

        // Assert
        consumerInstance.ReceivedMessages.ShouldHaveSingleItem();
        consumerInstance.ReceivedMessages[0].Content.ShouldBe("Hello, World!");
        _publisher.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompiledRpcHandlerShouldCallConsumerAndPublishResponse()
    {
        // Arrange
        var consumerInstance = new TestRequestConsumer();
        Use(consumerInstance);

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(RequestRegistration<TestRequestConsumer>());
        Use(queue);

        var plan = Target.GetPlan(new MessageType(typeof(TestRequest)).Name);
        var handler = plan!.Handlers.Single(h => h.Kind == HandlerKind.RequestConsumer);
        var testRequest = new TestRequest("Find users");
        var deliveryArgs = CreateDeliverEventArgs(replyTo: "reply.queue", correlationId: "corr-1");

        // Act
        await handler.DispatchAsync(_serviceProvider, testRequest, deliveryArgs, null, _publisher.PublishAsync, CancellationToken.None);

        // Assert
        consumerInstance.ReceivedRequests.ShouldHaveSingleItem();
        consumerInstance.ReceivedRequests[0].Query.ShouldBe("Find users");
        var published = _publisher.Published.ShouldHaveSingleItem();
        published.Exchange.ShouldBe(string.Empty);
        published.RoutingKey.ShouldBe("reply.queue");
        published.Properties.CorrelationId.ShouldBe("corr-1");

        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(published.Body.Span);
        envelope.ShouldNotBeNull();
        envelope.MessageType.ShouldBe(new MessageConfiguration(typeof(TestResponse).FullName!).Urn);
        envelope.RequestId.ShouldBe("corr-1");

        var response = envelope.Message.Deserialize<TestResponse>();
        response.ShouldNotBeNull();
        response.Result.ShouldBe("Processed: Find users");
    }

    [Fact]
    public async Task CompiledRpcHandlerShouldPublishFailureWhenConsumerThrows()
    {
        // Arrange
        UseReal<ThrowingRequestConsumer>();

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(RequestRegistration<ThrowingRequestConsumer>());
        Use(queue);

        var plan = Target.GetPlan(new MessageType(typeof(TestRequest)).Name);
        var handler = plan!.Handlers.Single(h => h.Kind == HandlerKind.RequestConsumer);

        // Act
        await handler.DispatchAsync(
            _serviceProvider,
            new TestRequest("throw"),
            CreateDeliverEventArgs(replyTo: "reply.queue"),
            null,
            _publisher.PublishAsync,
            CancellationToken.None);

        // Assert
        var published = _publisher.Published.ShouldHaveSingleItem();
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(published.Body.Span);
        envelope.ShouldNotBeNull();
        envelope.MessageType.ShouldBe(RpcFault.UrnUri);

        var fault = envelope.Message.Deserialize<RpcFault>();
        fault.ShouldNotBeNull();
        fault.Message.ShouldContain("failed to process request");
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
    public void HandlerFromFactoryCarriesItsKind()
    {
        // Arrange
        var factory = new RabbitMqHandlerFactory();

        // Act
        var entry = factory.ForRequestConsumer(typeof(TestRequestConsumer), typeof(TestRequest), typeof(TestResponse), retryPolicy: null);

        // Assert
        entry.Kind.ShouldBe(HandlerKind.RequestConsumer);
        entry.Handler.Kind.ShouldBe(HandlerKind.RequestConsumer);
        entry.Handler.Identity.ShouldBe($"{typeof(TestRequestConsumer).FullName}:{typeof(TestRequest).FullName}");
    }
}
