using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class MessageTypeCacheTests : BaseUnitTestCase
{
    private readonly Lazy<MessageTypeCache> _lazyTarget;
    private MessageTypeCache Target => _lazyTarget.Value;

    public MessageTypeCacheTests()
    {
        _lazyTarget = new Lazy<MessageTypeCache>(CreateInstance<MessageTypeCache>);
    }

    private static MessagingOptions CreateMessagingOptions() => new();

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

    #region Test Messages and Consumers

    private sealed record TestMessage(string Content);
    private sealed record TestRequest(string Query);
    private sealed record TestResponse(string Result);

    private sealed class TestMessageConsumer : IConsumer<TestMessage>
    {
        public List<TestMessage> ReceivedMessages { get; } = [];

        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            ReceivedMessages.Add(messageContext.Message);
            return Task.CompletedTask;
        }
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
    public void RegisterQueueShouldRegisterStandardConsumers()
    {
        // Arrange
        var consumer = new ConsumerType(typeof(TestMessageConsumer));
        var messageType = new MessageType(typeof(TestMessage));
        var registration = new ConsumerRegistration
        {
            ConsumerType = consumer,
            MessageType = messageType,
            RoutingKey = "test.message"
        };
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration);

        // Act
        Target.RegisterQueue(queue, CreateMessagingOptions());

        // Assert
        var plan = Target.GetPlan(messageType.Name);
        plan.ShouldNotBeNull();
        plan.StandardHandlers.ShouldHaveSingleItem();
        plan.StandardHandlers[0].RoutingKey.ShouldBe("test.message");
    }

    [Fact]
    public void RegisterQueueShouldRegisterRequestConsumers()
    {
        // Arrange
        var consumer = new ConsumerType(typeof(TestRequestConsumer));
        var messageType = new MessageType(typeof(TestRequest));
        var registration = new RequestConsumerRegistration
        {
            ConsumerType = consumer,
            MessageType = messageType,
            ResponseType = typeof(TestResponse),
            RoutingKey = "test.request"
        };
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration);

        // Act
        Target.RegisterQueue(queue, CreateMessagingOptions());

        // Assert
        var plan = Target.GetPlan(messageType.Name);
        plan.ShouldNotBeNull();
        plan.RpcHandler.ShouldNotBeNull();
        plan.RpcHandler.RoutingKey.ShouldBe("test.request");
    }

    [Fact]
    public async Task CompiledHandlerShouldCallConsumerWithCorrectMessage()
    {
        // Arrange
        var consumerInstance = new TestMessageConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        var serviceProvider = services.BuildServiceProvider();

        var consumer = new ConsumerType(typeof(TestMessageConsumer));
        var messageType = new MessageType(typeof(TestMessage));
        var registration = new ConsumerRegistration
        {
            ConsumerType = consumer,
            MessageType = messageType,
            RoutingKey = "#"
        };
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration);
        Target.RegisterQueue(queue, CreateMessagingOptions());

        var plan = Target.GetPlan(messageType.Name);
        var handler = plan!.StandardHandlers[0];
        var testMessage = new TestMessage("Hello, World!");

        // Act
        await handler.InvokeAsync(serviceProvider, testMessage, CreateDeliverEventArgs(), CancellationToken.None);

        // Assert
        consumerInstance.ReceivedMessages.ShouldHaveSingleItem();
        consumerInstance.ReceivedMessages[0].Content.ShouldBe("Hello, World!");
    }

    [Fact]
    public async Task CompiledRpcHandlerShouldCallConsumerAndPublishResponse()
    {
        // Arrange
        var consumerInstance = new TestRequestConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        var serviceProvider = services.BuildServiceProvider();

        var consumer = new ConsumerType(typeof(TestRequestConsumer));
        var messageType = new MessageType(typeof(TestRequest));
        var registration = new RequestConsumerRegistration
        {
            ConsumerType = consumer,
            MessageType = messageType,
            ResponseType = typeof(TestResponse),
            RoutingKey = "#"
        };
        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration);
        Target.RegisterQueue(queue, CreateMessagingOptions());

        var plan = Target.GetPlan(messageType.Name);
        var handler = plan!.RpcHandler!;
        var testRequest = new TestRequest("Find users");
        var deliveryArgs = CreateDeliverEventArgs(replyTo: "reply.queue", correlationId: "corr-1");

        var channel = GetMock<IChannel>();
        ReadOnlyMemory<byte> publishedBody = default;
        BasicProperties? publishedProperties = null;

        channel.Setup(x => x.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, string _, bool _, BasicProperties props, ReadOnlyMemory<byte> body, CancellationToken _) =>
            {
                publishedProperties = props;
                publishedBody = body;
            })
            .Returns(ValueTask.CompletedTask);

        // Act
        await handler.InvokeAsync(serviceProvider, testRequest, deliveryArgs, channel.Object, CancellationToken.None);

        // Assert
        consumerInstance.ReceivedRequests.ShouldHaveSingleItem();
        consumerInstance.ReceivedRequests[0].Query.ShouldBe("Find users");
        publishedProperties.ShouldNotBeNull();
        publishedProperties.CorrelationId.ShouldBe("corr-1");

        var messageResult = JsonSerializer.Deserialize<MessageResult>(publishedBody.Span);
        messageResult.ShouldNotBeNull();
        messageResult.IsSuccess.ShouldBeTrue();
        messageResult.Value.ShouldNotBeNull();

        var response = JsonSerializer.Deserialize<TestResponse>(messageResult.Value);
        response.ShouldNotBeNull();
        response.Result.ShouldBe("Processed: Find users");
    }

    [Fact]
    public async Task CompiledRpcHandlerShouldPublishFailureWhenConsumerThrows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ThrowingRequestConsumer>();
        var serviceProvider = services.BuildServiceProvider();

        var registration = new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(ThrowingRequestConsumer)),
            MessageType = new MessageType(typeof(TestRequest)),
            ResponseType = typeof(TestResponse),
            RoutingKey = "#"
        };

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration);
        Target.RegisterQueue(queue, CreateMessagingOptions());

        var plan = Target.GetPlan(new MessageType(typeof(TestRequest)).Name);
        var handler = plan!.RpcHandler!;

        var channel = GetMock<IChannel>();
        ReadOnlyMemory<byte> publishedBody = default;

        channel.Setup(x => x.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, string _, bool _, BasicProperties _, ReadOnlyMemory<byte> body, CancellationToken _) =>
            {
                publishedBody = body;
            })
            .Returns(ValueTask.CompletedTask);

        // Act
        await handler.InvokeAsync(
            serviceProvider,
            new TestRequest("throw"),
            CreateDeliverEventArgs(replyTo: "reply.queue"),
            channel.Object,
            CancellationToken.None);

        // Assert
        var messageResult = JsonSerializer.Deserialize<MessageResult>(publishedBody.Span);
        messageResult.ShouldNotBeNull();
        messageResult.IsSuccess.ShouldBeFalse();
        messageResult.ErrorMessage.ShouldContain("failed to process request");
    }

    [Fact]
    public void GetPlanShouldReturnNullForUnregisteredMessageType()
    {
        // Act
        var plan = Target.GetPlan("NonExistentMessage");

        // Assert
        plan.ShouldBeNull();
    }

    [Fact]
    public void RegisterQueueShouldSupportMultipleHandlersForSameMessageType()
    {
        // Arrange
        var consumer = new ConsumerType(typeof(TestMessageConsumer));
        var messageType = new MessageType(typeof(TestMessage));

        var registration1 = new ConsumerRegistration
        {
            ConsumerType = consumer,
            MessageType = messageType,
            RoutingKey = "route.1"
        };
        var registration2 = new ConsumerRegistration
        {
            ConsumerType = consumer,
            MessageType = messageType,
            RoutingKey = "route.2"
        };

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(registration1);
        queue.AddConsumer(registration2);

        // Act
        Target.RegisterQueue(queue, CreateMessagingOptions());

        // Assert
        var plan = Target.GetPlan(messageType.Name);
        plan.ShouldNotBeNull();
        plan.StandardHandlers.Count.ShouldBe(2);
        plan.StandardHandlers[0].RoutingKey.ShouldBe("route.1");
        plan.StandardHandlers[1].RoutingKey.ShouldBe("route.2");
    }

    [Fact]
    public void RpcHandlerRoutingKeyShouldReturnHandlerRoutingKey()
    {
        // Arrange
        var messageType = new MessageType(typeof(TestRequest));
        var plan = new MessageExecutionPlan(messageType)
        {
            RpcHandler = new RpcInvoker<TestRequestConsumer, TestRequest, TestResponse>(CreateMessagingOptions(), "custom.routing.key", null)
        };

        // Act & Assert
        plan.RpcHandler.RoutingKey.ShouldBe("custom.routing.key");
    }
}
