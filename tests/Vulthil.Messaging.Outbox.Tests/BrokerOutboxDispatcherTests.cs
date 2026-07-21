using System.Text.Json;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Outbox.Tests;

public sealed class BrokerOutboxDispatcherTests : BaseUnitTestCase
{
    [Fact]
    public async Task DispatchAsyncThrowsDescriptiveErrorWhenMessageTypeCannotBeResolved()
    {
        // Arrange
        var dispatcher = CreateInstance<BrokerOutboxDispatcher>();
        var message = new OutboxMessageData(
            Id: Guid.NewGuid(),
            Type: "Vulthil.Nonexistent.PhantomMessage",
            Content: "{}",
            TraceParent: null,
            TraceState: null,
            Destination: OutboxDestination.Publish,
            Metadata: null);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(message, CancellationToken));

        // Assert
        exception.Message.ShouldContain("Vulthil.Nonexistent.PhantomMessage");
    }

    [Fact]
    public async Task DispatchAsyncPublishesThePayloadWithTheCapturedMessageIdCorrelationIdRoutingKeyAndHeaders()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        GetMock<IMessageConfigurationProvider>().Setup(provider => provider.JsonSerializerOptions).Returns(jsonOptions);
        Func<IPublishContext, ValueTask>? capturedConfigure = null;
        GetMock<ITransportPublisher>()
            .Setup(publisher => publisher.PublishAsync(It.IsAny<TestMessage>(), It.IsAny<Func<IPublishContext, ValueTask>>(), It.IsAny<CancellationToken>()))
            .Callback<TestMessage, Func<IPublishContext, ValueTask>, CancellationToken>((_, configure, _) => capturedConfigure = configure)
            .Returns(Task.CompletedTask);
        var metadata = new BrokerOutboxMetadata
        {
            MessageId = "stable-message-id",
            CorrelationId = "correlation-1",
            RoutingKey = "routing-key-1",
            Headers = new Dictionary<string, object?> { ["tenant"] = "acme" },
        };
        var dispatcher = CreateInstance<BrokerOutboxDispatcher>();
        var message = new OutboxMessageData(
            Id: Guid.NewGuid(),
            Type: typeof(TestMessage).FullName!,
            Content: JsonSerializer.Serialize(new TestMessage("hello"), jsonOptions),
            TraceParent: null,
            TraceState: null,
            Destination: OutboxDestination.Publish,
            Metadata: JsonSerializer.Serialize(metadata, jsonOptions));

        // Act
        await dispatcher.DispatchAsync(message, CancellationToken);

        // Assert
        GetMock<ITransportPublisher>().Verify(
            publisher => publisher.PublishAsync(It.Is<TestMessage>(m => m.Value == "hello"), It.IsAny<Func<IPublishContext, ValueTask>>(), CancellationToken),
            Times.Once);
        capturedConfigure.ShouldNotBeNull();
        var appliedContext = new PublishContext();
        await capturedConfigure!(appliedContext);
        appliedContext.MessageId.ShouldBe("stable-message-id");
        appliedContext.CorrelationId.ShouldBe("correlation-1");
        appliedContext.RoutingKey.ShouldBe("routing-key-1");
        appliedContext.Headers["tenant"].ShouldBe("acme");
    }

    [Fact]
    public async Task DispatchAsyncSendsThePayloadToTheCapturedDestinationAddressWithFidelity()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        GetMock<IMessageConfigurationProvider>().Setup(provider => provider.JsonSerializerOptions).Returns(jsonOptions);
        var sendEndpoint = new Mock<ISendEndpoint>();
        Func<IPublishContext, ValueTask>? capturedConfigure = null;
        sendEndpoint
            .Setup(endpoint => endpoint.SendAsync(It.IsAny<TestMessage>(), It.IsAny<Func<IPublishContext, ValueTask>>(), It.IsAny<CancellationToken>()))
            .Callback<TestMessage, Func<IPublishContext, ValueTask>, CancellationToken>((_, configure, _) => capturedConfigure = configure)
            .Returns(Task.CompletedTask);
        var destinationAddress = new Uri("queue:order-commands");
        GetMock<ITransportSendEndpointProvider>()
            .Setup(provider => provider.GetSendEndpointAsync(destinationAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendEndpoint.Object);
        var metadata = new BrokerOutboxMetadata
        {
            MessageId = "stable-send-id",
            DestinationAddress = destinationAddress.ToString(),
            Headers = new Dictionary<string, object?> { ["tenant"] = "acme" },
        };
        var dispatcher = CreateInstance<BrokerOutboxDispatcher>();
        var message = new OutboxMessageData(
            Id: Guid.NewGuid(),
            Type: typeof(TestMessage).FullName!,
            Content: JsonSerializer.Serialize(new TestMessage("hello"), jsonOptions),
            TraceParent: null,
            TraceState: null,
            Destination: OutboxDestination.Send,
            Metadata: JsonSerializer.Serialize(metadata, jsonOptions));

        // Act
        await dispatcher.DispatchAsync(message, CancellationToken);

        // Assert
        sendEndpoint.Verify(
            endpoint => endpoint.SendAsync(It.Is<TestMessage>(m => m.Value == "hello"), It.IsAny<Func<IPublishContext, ValueTask>>(), CancellationToken),
            Times.Once);
        capturedConfigure.ShouldNotBeNull();
        var appliedContext = new PublishContext();
        await capturedConfigure!(appliedContext);
        appliedContext.MessageId.ShouldBe("stable-send-id");
        appliedContext.Headers["tenant"].ShouldBe("acme");
    }

    [Fact]
    public async Task DispatchAsyncThrowsDescriptiveErrorWhenASendMessageHasNoDestinationAddress()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        GetMock<IMessageConfigurationProvider>().Setup(provider => provider.JsonSerializerOptions).Returns(jsonOptions);
        var dispatcher = CreateInstance<BrokerOutboxDispatcher>();
        var message = new OutboxMessageData(
            Id: Guid.NewGuid(),
            Type: typeof(TestMessage).FullName!,
            Content: JsonSerializer.Serialize(new TestMessage("hello"), jsonOptions),
            TraceParent: null,
            TraceState: null,
            Destination: OutboxDestination.Send,
            Metadata: null);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(message, CancellationToken));

        // Assert
        exception.Message.ShouldContain("missing its destination address");
        GetMock<ITransportSendEndpointProvider>().Verify(
            provider => provider.GetSendEndpointAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed record TestMessage(string Value);
}
