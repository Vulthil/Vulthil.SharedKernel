using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class ConsumeFilterPipelineTests : BaseUnitTestCase
{
    private readonly Lazy<MessageTypeCache> _lazyTarget;
    private MessageTypeCache Target => _lazyTarget.Value;

    public ConsumeFilterPipelineTests()
    {
        _lazyTarget = new Lazy<MessageTypeCache>(CreateInstance<MessageTypeCache>);
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

    internal sealed record TestMessage(string Content);
    internal sealed record TestRequest(string Query);
    internal sealed record TestResponse(string Result);

    private sealed class RecordingConsumer : IConsumer<TestMessage>
    {
        public List<TestMessage> Received { get; } = [];

        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            Received.Add(messageContext.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public List<TestRequest> Received { get; } = [];

        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
        {
            Received.Add(messageContext.Message);
            return Task.FromResult(new TestResponse($"Processed: {messageContext.Message.Query}"));
        }
    }

    private sealed class RecordingFilter<TMessage>(List<string> trace, string name) : IConsumeFilter<TMessage>
        where TMessage : notnull
    {
        public List<string> Trace { get; } = trace;
        public string Name { get; } = name;
        public bool ShortCircuit { get; set; }

        public async Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
        {
            Trace.Add($"{Name}:before");

            if (ShortCircuit)
            {
                Trace.Add($"{Name}:short-circuit");
                return;
            }

            await next(context);
            Trace.Add($"{Name}:after");
        }
    }

    [Fact]
    public async Task PipelineWithNoFiltersInvokesConsumerDirectly()
    {
        // Arrange
        var consumerInstance = new RecordingConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        services.AddSingleton(Mock.Of<IPublisher>());
        var serviceProvider = services.BuildServiceProvider();

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(RecordingConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
            RoutingKey = "#"
        });
        Target.RegisterQueue(queue);

        var handler = Target.GetPlan(new MessageType(typeof(TestMessage)).Name)!.StandardHandlers[0];

        // Act
        await handler.InvokeAsync(serviceProvider, new TestMessage("payload"), CreateDeliverEventArgs(), CancellationToken.None);

        // Assert
        consumerInstance.Received.ShouldHaveSingleItem().Content.ShouldBe("payload");
    }

    [Fact]
    public async Task PipelineComposesFiltersInRegistrationOrderOutermostFirst()
    {
        // Arrange
        var trace = new List<string>();
        var consumerInstance = new RecordingConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        services.AddSingleton(Mock.Of<IPublisher>());
        // Order matters: First registered should be outermost.
        services.AddScoped<IConsumeFilter<TestMessage>>(_ => new RecordingFilter<TestMessage>(trace, "outer"));
        services.AddScoped<IConsumeFilter<TestMessage>>(_ => new RecordingFilter<TestMessage>(trace, "inner"));
        var serviceProvider = services.BuildServiceProvider();

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(RecordingConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
            RoutingKey = "#"
        });
        Target.RegisterQueue(queue);

        var handler = Target.GetPlan(new MessageType(typeof(TestMessage)).Name)!.StandardHandlers[0];

        // Act
        await handler.InvokeAsync(serviceProvider, new TestMessage("payload"), CreateDeliverEventArgs(), CancellationToken.None);

        // Assert
        trace.ShouldBe(["outer:before", "inner:before", "inner:after", "outer:after"]);
        consumerInstance.Received.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task FilterShortCircuitPreventsConsumerInvocation()
    {
        // Arrange
        var trace = new List<string>();
        var consumerInstance = new RecordingConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddScoped<IConsumeFilter<TestMessage>>(_ =>
            new RecordingFilter<TestMessage>(trace, "gate") { ShortCircuit = true });
        var serviceProvider = services.BuildServiceProvider();

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(RecordingConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
            RoutingKey = "#"
        });
        Target.RegisterQueue(queue);

        var handler = Target.GetPlan(new MessageType(typeof(TestMessage)).Name)!.StandardHandlers[0];

        // Act
        await handler.InvokeAsync(serviceProvider, new TestMessage("payload"), CreateDeliverEventArgs(), CancellationToken.None);

        // Assert
        trace.ShouldBe(["gate:before", "gate:short-circuit"]);
        consumerInstance.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task RpcPipelineComposesFiltersAroundConsumerCall()
    {
        // Arrange
        var trace = new List<string>();
        var consumerInstance = new RecordingRequestConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<IMessageConfigurationProvider>(new MessageConfigurationProvider(new MessagingOptions()));
        services.AddScoped<IConsumeFilter<TestRequest>>(_ => new RecordingFilter<TestRequest>(trace, "log"));
        var serviceProvider = services.BuildServiceProvider();

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(RecordingRequestConsumer)),
            MessageType = new MessageType(typeof(TestRequest)),
            ResponseType = typeof(TestResponse),
            RoutingKey = "#"
        });
        Target.RegisterQueue(queue);

        var handler = Target.GetPlan(new MessageType(typeof(TestRequest)).Name)!.RpcHandler!;

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
            new TestRequest("query"),
            CreateDeliverEventArgs(replyTo: "reply", correlationId: "corr-1"),
            channel.Object,
            CancellationToken.None);

        // Assert
        trace.ShouldBe(["log:before", "log:after"]);
        consumerInstance.Received.ShouldHaveSingleItem();

        var envelope = JsonSerializer.Deserialize<MessageResult>(publishedBody.Span);
        envelope.ShouldNotBeNull();
        envelope.IsSuccess.ShouldBeTrue();
        var response = JsonSerializer.Deserialize<TestResponse>(envelope.Value);
        response!.Result.ShouldBe("Processed: query");
    }

    [Fact]
    public async Task RpcPipelineShortCircuitProducesFailureResponse()
    {
        // Arrange
        var consumerInstance = new RecordingRequestConsumer();
        var services = new ServiceCollection();
        services.AddScoped(_ => consumerInstance);
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<IMessageConfigurationProvider>(new MessageConfigurationProvider(new MessagingOptions()));
        services.AddScoped<IConsumeFilter<TestRequest>>(_ =>
            new RecordingFilter<TestRequest>([], "gate") { ShortCircuit = true });
        var serviceProvider = services.BuildServiceProvider();

        var queue = new QueueDefinition("TestQueue");
        queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(RecordingRequestConsumer)),
            MessageType = new MessageType(typeof(TestRequest)),
            ResponseType = typeof(TestResponse),
            RoutingKey = "#"
        });
        Target.RegisterQueue(queue);

        var handler = Target.GetPlan(new MessageType(typeof(TestRequest)).Name)!.RpcHandler!;

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
            new TestRequest("query"),
            CreateDeliverEventArgs(replyTo: "reply"),
            channel.Object,
            CancellationToken.None);

        // Assert
        consumerInstance.Received.ShouldBeEmpty();

        var envelope = JsonSerializer.Deserialize<MessageResult>(publishedBody.Span);
        envelope.ShouldNotBeNull();
        envelope.IsSuccess.ShouldBeFalse();
        envelope.ErrorMessage.ShouldContain("short-circuit");
    }
}
