using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class MessageTypeCacheTests : BaseUnitTestCase
{
    private static MessageTypeCache CreateTarget() => new();

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
        var target = CreateTarget();
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
        target.RegisterQueue(queue);

        // Assert
        var plan = target.GetPlan(messageType.Name);
        plan.ShouldNotBeNull();
        plan.StandardHandlers.ShouldHaveSingleItem();
        plan.StandardHandlers[0].RoutingKey.ShouldBe("test.message");
    }

    [Fact]
    public void RegisterQueueShouldRegisterRequestConsumers()
    {
        // Arrange
        var target = CreateTarget();
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
        target.RegisterQueue(queue);

        // Assert
        var plan = target.GetPlan(messageType.Name);
        plan.ShouldNotBeNull();
        plan.RpcHandler.ShouldNotBeNull();
        plan.RpcHandler.RoutingKey.ShouldBe("test.request");
    }

    [Fact]
    public async Task CompiledHandlerShouldCallConsumerWithCorrectMessage()
    {
        // Arrange
        var target = CreateTarget();
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
        target.RegisterQueue(queue);

        var plan = target.GetPlan(messageType.Name);
        var handler = plan!.StandardHandlers[0].Handler;
        var testMessage = new TestMessage("Hello, World!");
        var messageContext = new RabbitMqMessageContext("test-id", "test.message", new Dictionary<string, object?>());

        // Act
        await handler(serviceProvider, testMessage, messageContext, CancellationToken.None);

        // Assert
        consumerInstance.ReceivedMessages.ShouldHaveSingleItem();
        consumerInstance.ReceivedMessages[0].Content.ShouldBe("Hello, World!");
    }

    [Fact]
    public async Task CompiledRpcHandlerShouldCallConsumerAndReturnResponse()
    {
        // Arrange
        var target = CreateTarget();
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
        target.RegisterQueue(queue);

        var plan = target.GetPlan(messageType.Name);
        var handler = plan!.RpcHandler!.Handler;
        var testRequest = new TestRequest("Find users");
        var messageContext = new RabbitMqMessageContext("test-id", "test.request", new Dictionary<string, object?>());

        // Act
        var result = await handler(serviceProvider, testRequest, messageContext, CancellationToken.None);

        // Assert
        consumerInstance.ReceivedRequests.ShouldHaveSingleItem();
        consumerInstance.ReceivedRequests[0].Query.ShouldBe("Find users");
        result.ShouldNotBeNull();
        var testResult = result.ShouldBeOfType<TestResponse>();
        testResult.Result.ShouldBe("Processed: Find users");
    }

    [Fact]
    public void GetPlanShouldReturnNullForUnregisteredMessageType()
    {
        // Arrange
        var target = CreateTarget();

        // Act
        var plan = target.GetPlan("NonExistentMessage");

        // Assert
        plan.ShouldBeNull();
    }

    [Fact]
    public void RegisterQueueShouldSupportMultipleHandlersForSameMessageType()
    {
        // Arrange
        var target = CreateTarget();
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
        target.RegisterQueue(queue);

        // Assert
        var plan = target.GetPlan(messageType.Name);
        plan.ShouldNotBeNull();
        plan.StandardHandlers.Count.ShouldBe(2);
        plan.StandardHandlers[0].RoutingKey.ShouldBe("route.1");
        plan.StandardHandlers[1].RoutingKey.ShouldBe("route.2");
    }

    [Fact]
    public void RpcHandlerRoutingKeyShouldDefaultToHashWhenRpcHandlerIsNull()
    {
        // Arrange
        var messageType = new MessageType(typeof(TestMessage));
        var plan = new MessageExecutionPlan(messageType);

        // Act & Assert
        plan.RpcHandlerRoutingKey.ShouldBe("#");
    }

    [Fact]
    public void RpcHandlerRoutingKeyShouldReturnHandlerRoutingKey()
    {
        // Arrange
        var messageType = new MessageType(typeof(TestRequest));
        var plan = new MessageExecutionPlan(messageType);

        // Create a mock RPC handler
        static async Task<object> MockHandler(IServiceProvider _, object __, IMessageContext ___, CancellationToken ____)
        {
            return await Task.FromResult(new TestResponse("test"));
        }

        plan.RpcHandler = new RpcHandlerEntry(MockHandler, "custom.routing.key");

        // Act & Assert
        plan.RpcHandlerRoutingKey.ShouldBe("custom.routing.key");
    }

    #region Helper Types

    private sealed record RabbitMqMessageContext(string CorrelationId, string RoutingKey, IDictionary<string, object?> Headers) : IMessageContext;

    #endregion
}
