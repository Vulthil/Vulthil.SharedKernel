using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Verifies that IMessageContext.SendAsync auto-propagates correlation metadata in the same way as PublishAsync.
/// </summary>
public sealed class MessageContextSendTests : BaseUnitTestCase
{
    private sealed record TestMessage(string Content);

    /// <summary>
    /// Verifies that CorrelationId, ConversationId, and InitiatorId from the incoming context are propagated to the outgoing send.
    /// </summary>
    [Fact]
    public async Task SendAsyncShouldPropagateCorrelationMetadata()
    {
        // Arrange
        var endpointMock = new Mock<ISendEndpoint>();
        var capturedPublishContext = new PublishContext();
        endpointMock
            .Setup(e => e.SendAsync(
                It.IsAny<TestMessage>(),
                It.IsAny<Func<IPublishContext, ValueTask>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<TestMessage, Func<IPublishContext, ValueTask>?, CancellationToken>(
                async (_, configure, _) =>
                {
                    if (configure is not null)
                    {
                        await configure(capturedPublishContext);
                    }
                });

        var providerMock = new Mock<ISendEndpointProvider>();
        providerMock
            .Setup(p => p.GetSendEndpointAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Returns<Uri, CancellationToken>((uri, _) => ValueTask.FromResult<ISendEndpoint>(endpointMock.Object));

        var context = CreateTypedContext(
            providerMock.Object,
            correlationId: "corr-1",
            conversationId: "conv-1",
            messageId: "msg-1");

        // Act
        await context.SendAsync(new Uri("queue:dest"), new TestMessage("payload"));

        // Assert
        capturedPublishContext.CorrelationId.ShouldBe("corr-1");
        capturedPublishContext.ConversationId.ShouldBe("conv-1");
        capturedPublishContext.InitiatorId.ShouldBe("msg-1");
    }

    /// <summary>
    /// Verifies that an explicit configure callback overrides auto-propagated values.
    /// </summary>
    [Fact]
    public async Task SendAsyncShouldLetExplicitConfigureOverrideAutoPropagation()
    {
        // Arrange
        var endpointMock = new Mock<ISendEndpoint>();
        var capturedPublishContext = new PublishContext();
        endpointMock
            .Setup(e => e.SendAsync(
                It.IsAny<TestMessage>(),
                It.IsAny<Func<IPublishContext, ValueTask>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<TestMessage, Func<IPublishContext, ValueTask>?, CancellationToken>(
                async (_, configure, _) =>
                {
                    if (configure is not null)
                    {
                        await configure(capturedPublishContext);
                    }
                });

        var providerMock = new Mock<ISendEndpointProvider>();
        providerMock
            .Setup(p => p.GetSendEndpointAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Returns<Uri, CancellationToken>((_, _) => ValueTask.FromResult<ISendEndpoint>(endpointMock.Object));

        var context = CreateTypedContext(providerMock.Object, correlationId: "auto-corr");

        // Act
        await context.SendAsync(
            new Uri("queue:dest"),
            new TestMessage("payload"),
            ctx =>
            {
                ctx.SetCorrelationId("explicit-corr");
                return ValueTask.CompletedTask;
            });

        // Assert
        capturedPublishContext.CorrelationId.ShouldBe("explicit-corr");
    }

    /// <summary>
    /// Verifies that a null destination address throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task SendAsyncWithNullDestinationThrows()
    {
        // Arrange
        var providerMock = new Mock<ISendEndpointProvider>();
        var context = CreateTypedContext(providerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => context.SendAsync<TestMessage>(null!, new TestMessage("x")));
    }

    private static MessageContext<TestMessage> CreateTypedContext(
        ISendEndpointProvider sendEndpointProvider,
        string correlationId = "corr-1",
        string? conversationId = null,
        string? messageId = null)
    {
        var headers = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(conversationId))
        {
            headers["ConversationId"] = System.Text.Encoding.UTF8.GetBytes(conversationId);
        }

        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            MessageId = messageId ?? "msg-id",
            Headers = headers,
        };
        var ea = new BasicDeliverEventArgs(
            "consumer-tag",
            1,
            false,
            "exchange",
            "routing.key",
            props,
            ReadOnlyMemory<byte>.Empty);

        return MessageContext.CreateContext(new TestMessage("payload"), ea, NullPublisher.Instance, sendEndpointProvider, CancellationToken.None);
    }
}
