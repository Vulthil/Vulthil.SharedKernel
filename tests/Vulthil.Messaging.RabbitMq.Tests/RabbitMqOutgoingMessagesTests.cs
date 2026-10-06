using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Pins the wire shape of every outgoing AMQP message: each test compares one <see cref="WireRow"/> — the route and the
/// properties that make up the message — against the shape its operation must have.
/// </summary>
public sealed class RabbitMqOutgoingMessagesTests : BaseUnitTestCase
{
    private const string OrdersExchange = "orders.exchange";
    private const string FaultExchange = "Fault.Exchange";
    private const string MessageUrn = "urn:message:Acme.Orders:OrderCreatedEvent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void APublishGoesToTheMessageTypesExchangeAsAPersistentMessage()
    {
        // Arrange
        var context = new PublishContext();
        context.SetCorrelationId("order-42");
        context.SetResponseAddress(new Uri("queue:replies"));
        context.AddHeader("tenant", "acme");
        var configuration = NewConfiguration();

        // Act
        var publish = RabbitMqOutgoingMessages.Publish(new OrderPlaced("A-1"), context, configuration, JsonOptions);

        // Assert
        WireRow.Of(publish.Message).ShouldBe(new WireRow(
            OrdersExchange, RoutingKey: "", Mandatory: false, configuration.Urn.AbsoluteUri, "application/json",
            CorrelationId: "order-42", ReplyTo: "replies", Persistent: true, Expiration: null, HasMessageId: true, HasTimestamp: true));
        publish.Operation.ShouldBe("publish");
        publish.Destination.ShouldBe(OrdersExchange);
        publish.Message.Properties.MessageId.ShouldBe(publish.Ids.MessageId);
        publish.Message.Properties.Headers.ShouldNotBeNull().ShouldContainKeyAndValue("tenant", "acme");
        var envelope = Envelope(publish.Message);
        envelope.MessageId.ShouldBe(publish.Ids.MessageId);
        envelope.CorrelationId.ShouldBe("order-42");
        envelope.RequestId.ShouldBeNull();
        envelope.Message.GetProperty("orderId").GetString().ShouldBe("A-1");
    }

    [Theory]
    [InlineData("from-context", "from-formatter", "from-context")]
    [InlineData(null, "from-formatter", "from-formatter")]
    [InlineData(null, null, "")]
    public void APublishOrARequestIsRoutedByTheContextKeyElseTheFormatterElseTheEmptyKey(string? contextKey, string? formatterKey, string expected)
    {
        // Arrange
        var publishContext = new PublishContext();
        var requestContext = new RequestContext();
        if (contextKey is not null)
        {
            publishContext.SetRoutingKey(contextKey);
            requestContext.SetRoutingKey(contextKey);
        }

        var configuration = NewConfiguration();
        if (formatterKey is not null)
        {
            configuration.RoutingKeyFormatter = _ => formatterKey;
        }

        // Act
        var publish = RabbitMqOutgoingMessages.Publish(new OrderPlaced("A-1"), publishContext, configuration, JsonOptions);
        var request = RabbitMqOutgoingMessages.Request(new OrderPlaced("A-1"), requestContext, configuration, "request-1", "reply-queue", TimeSpan.FromSeconds(5), JsonOptions);

        // Assert
        publish.Message.RoutingKey.ShouldBe(expected);
        request.Message.RoutingKey.ShouldBe(expected);
    }

    [Theory]
    [InlineData("from-context", "from-formatter", "from-context")]
    [InlineData(null, "from-formatter", "from-formatter")]
    public void TheCorrelationIdIsTheContextsElseTheFormatters(string? contextId, string formatterId, string expected)
    {
        // Arrange
        var context = new PublishContext();
        if (contextId is not null)
        {
            context.SetCorrelationId(contextId);
        }

        var configuration = NewConfiguration();
        configuration.CorrelationIdFormatter = _ => formatterId;

        // Act
        var ids = RabbitMqOutgoingMessages.ResolveIds(new OrderPlaced("A-1"), context, configuration);

        // Assert
        ids.CorrelationId.ShouldBe(expected);
    }

    [Fact]
    public void WithoutAContextOrFormatterValueTheIdsAreFresh()
    {
        // Act
        var first = RabbitMqOutgoingMessages.ResolveIds(new OrderPlaced("A-1"), new PublishContext(), NewConfiguration());
        var second = RabbitMqOutgoingMessages.ResolveIds(new OrderPlaced("A-1"), new PublishContext(), NewConfiguration());

        // Assert
        first.CorrelationId.ShouldNotBeNullOrEmpty();
        first.MessageId.ShouldNotBeNullOrEmpty();
        first.CorrelationId.ShouldNotBe(second.CorrelationId);
        first.MessageId.ShouldNotBe(second.MessageId);
    }

    [Fact]
    public void ASendGoesToItsQueueThroughTheDefaultExchangeAndIgnoresTheRoutingKeyFormatter()
    {
        // Arrange
        var context = new PublishContext();
        context.SetCorrelationId("order-42");
        var configuration = NewConfiguration();
        configuration.RoutingKeyFormatter = _ => "ignored";

        // Act
        var send = RabbitMqOutgoingMessages.Send(new OrderPlaced("A-1"), context, configuration, "billing", JsonOptions);

        // Assert
        WireRow.Of(send.Message).ShouldBe(new WireRow(
            Exchange: "", RoutingKey: "billing", Mandatory: true, configuration.Urn.AbsoluteUri, "application/json",
            CorrelationId: "order-42", ReplyTo: null, Persistent: true, Expiration: null, HasMessageId: true, HasTimestamp: true));
        send.Operation.ShouldBe("send");
        send.Destination.ShouldBe("billing");
    }

    [Fact]
    public void ARequestCarriesItsRequestIdAsTheCorrelationIdAndExpiresAfterTheTimeout()
    {
        // Arrange
        var context = new RequestContext();
        context.SetCorrelationId("order-42");
        var configuration = NewConfiguration();

        // Act
        var request = RabbitMqOutgoingMessages.Request(new OrderPlaced("A-1"), context, configuration, "request-1", "reply-queue", TimeSpan.FromSeconds(5), JsonOptions);

        // Assert
        WireRow.Of(request.Message).ShouldBe(new WireRow(
            OrdersExchange, RoutingKey: "", Mandatory: false, configuration.Urn.AbsoluteUri, "application/json",
            CorrelationId: "request-1", ReplyTo: "reply-queue", Persistent: false, Expiration: "5000", HasMessageId: true, HasTimestamp: true));
        request.Operation.ShouldBe("request");
        request.Ids.CorrelationId.ShouldBe("order-42");
        var envelope = Envelope(request.Message);
        envelope.RequestId.ShouldBe("request-1");
        envelope.CorrelationId.ShouldBe("order-42");
    }

    [Fact]
    public void ARequestRepliesToTheContextsResponseAddressInsteadOfTheReplyQueue()
    {
        // Arrange
        var context = new RequestContext();
        context.SetResponseAddress(new Uri("queue:custom-replies"));

        // Act
        var request = RabbitMqOutgoingMessages.Request(new OrderPlaced("A-1"), context, NewConfiguration(), "request-1", "reply-queue", TimeSpan.FromSeconds(5), JsonOptions);

        // Assert
        request.Message.Properties.ReplyTo.ShouldBe("custom-replies");
    }

    [Fact]
    public void ARequestThatWaitsIndefinitelyNeverExpires()
    {
        // Act
        var request = RabbitMqOutgoingMessages.Request(new OrderPlaced("A-1"), new RequestContext(), NewConfiguration(), "request-1", "reply-queue", Timeout.InfiniteTimeSpan, JsonOptions);

        // Assert
        request.Message.Properties.Expiration.ShouldBeNull();
    }

    [Fact]
    public void ARetryRePublishesTheDeliveryToTheRetryExchangeWithTheRoundTheHandlersAndTheDelayAsTtl()
    {
        // Arrange
        var delivery = Delivery(new BasicProperties
        {
            Type = MessageUrn,
            MessageId = "message-1",
            CorrelationId = "order-42",
            Headers = new Dictionary<string, object?> { ["tenant"] = "acme" },
        });

        // Act
        var retry = RabbitMqOutgoingMessages.Retry(delivery, "orders", retryCount: 2, ["orders:OrderPlaced"], TimeSpan.FromSeconds(3));

        // Assert
        WireRow.Of(retry).ShouldBe(new WireRow(
            "orders.Retry", RoutingKey: "order.placed", Mandatory: true, MessageUrn, ContentType: null,
            CorrelationId: "order-42", ReplyTo: null, Persistent: false, Expiration: "3000", HasMessageId: true, HasTimestamp: false));
        retry.Properties.MessageId.ShouldBe("message-1");
        var headers = retry.Properties.Headers.ShouldNotBeNull();
        headers[RabbitMqConstants.RetryCountHeader].ShouldBe(2);
        RabbitMqConstants.GetRetryHandlerIdentities(headers).ShouldBe(["orders:OrderPlaced"]);
        headers.ShouldContainKeyAndValue("tenant", "acme");
        delivery.BasicProperties.Headers.ShouldNotBeNull().ShouldNotContainKey(RabbitMqConstants.RetryCountHeader);
        retry.Body.ToArray().ShouldBe(delivery.Body.ToArray());
    }

    [Fact]
    public void AFaultGoesToTheFaultExchangeWithThePayloadAsDelivered()
    {
        // Arrange
        var delivery = Delivery(new BasicProperties { Type = MessageUrn, CorrelationId = "order-42" });
        var envelopeMessage = JsonSerializer.SerializeToElement(new { orderId = "A-1", extra = "kept" });

        // Act
        var fault = RabbitMqOutgoingMessages.Fault(NewFault(), envelopeMessage, delivery, FaultExchange, MessageUrn, JsonOptions);

        // Assert
        WireRow.Of(fault).ShouldBe(new WireRow(
            FaultExchange, RoutingKey: MessageUrn, Mandatory: false, $"Fault<{MessageUrn}>", "application/json",
            CorrelationId: "order-42", ReplyTo: null, Persistent: false, Expiration: null, HasMessageId: true, HasTimestamp: true));
        fault.Properties.Headers.ShouldBeNull();
        var published = JsonSerializer.Deserialize<Fault<JsonElement>>(fault.Body.Span, JsonOptions).ShouldNotBeNull();
        published.Message.GetProperty("extra").GetString().ShouldBe("kept");
        published.ExceptionMessage.ShouldBe("boom");
    }

    [Fact]
    public void AFaultOfABareDeliveryCarriesTheWholeBody()
    {
        // Arrange
        var delivery = Delivery(new BasicProperties { Type = MessageUrn }, JsonSerializer.SerializeToUtf8Bytes(new { orderId = "B-2" }));

        // Act
        var fault = RabbitMqOutgoingMessages.Fault(NewFault(), envelopeMessage: null, delivery, FaultExchange, MessageUrn, JsonOptions);

        // Assert
        var published = JsonSerializer.Deserialize<Fault<JsonElement>>(fault.Body.Span, JsonOptions).ShouldNotBeNull();
        published.Message.GetProperty("orderId").GetString().ShouldBe("B-2");
    }

    [Fact]
    public void ResolveFaultRouteBroadcastsToTheFaultExchangeWhenNoFaultAddressIsPresent()
    {
        // Act
        var (exchange, routingKey) = RabbitMqOutgoingMessages.ResolveFaultRoute(faultAddress: null, FaultExchange, MessageUrn);

        // Assert
        exchange.ShouldBe(FaultExchange);
        routingKey.ShouldBe(MessageUrn);
    }

    [Fact]
    public void ResolveFaultRouteRoutesPointToPointThroughTheDefaultExchangeWhenFaultAddressIsPresent()
    {
        // Act
        var (exchange, routingKey) = RabbitMqOutgoingMessages.ResolveFaultRoute(new Uri("queue:order-faults"), FaultExchange, MessageUrn);

        // Assert
        exchange.ShouldBe(string.Empty);
        routingKey.ShouldBe("order-faults");
    }

    [Fact]
    public void AFaultGoesToTheFaultAddressOfItsOriginalContextNotToOneInTheDeliveryHeaders()
    {
        // Arrange
        var delivery = Delivery(new BasicProperties
        {
            Type = MessageUrn,
            Headers = new Dictionary<string, object?> { ["FaultAddress"] = Encoding.UTF8.GetBytes("queue:header-faults") },
        });

        // Act
        var fault = RabbitMqOutgoingMessages.Fault(
            NewFault(new Uri("queue:order-faults")), envelopeMessage: null, delivery, FaultExchange, MessageUrn, JsonOptions);

        // Assert
        fault.Exchange.ShouldBe(string.Empty);
        fault.RoutingKey.ShouldBe("order-faults");
    }

    [Fact]
    public void AReplyGoesToTheReplyToQueueUnderTheRequestsCorrelationId()
    {
        // Arrange
        var delivery = Delivery(new BasicProperties { Type = MessageUrn, CorrelationId = "request-1", ReplyTo = "reply-queue" });
        var reply = new MessageEnvelope
        {
            MessageId = "reply-1",
            MessageType = new Uri("urn:message:Acme.Orders:OrderAccepted"),
            Message = JsonSerializer.SerializeToElement(new { accepted = true }),
        };

        // Act
        var replyMessage = RabbitMqOutgoingMessages.Reply(reply, delivery, JsonOptions).ShouldNotBeNull();

        // Assert
        WireRow.Of(replyMessage).ShouldBe(new WireRow(
            Exchange: "", RoutingKey: "reply-queue", Mandatory: true, "urn:message:Acme.Orders:OrderAccepted", "application/json",
            CorrelationId: "request-1", ReplyTo: null, Persistent: false, Expiration: null, HasMessageId: true, HasTimestamp: true));
        replyMessage.Properties.MessageId.ShouldBe("reply-1");
    }

    [Fact]
    public void ARequestWithoutAReplyToGetsNoReply()
    {
        // Arrange
        var delivery = Delivery(new BasicProperties { Type = MessageUrn, CorrelationId = "request-1" });
        var reply = new MessageEnvelope { MessageType = new Uri("urn:message:Acme.Orders:OrderAccepted"), Message = JsonSerializer.SerializeToElement(new { }) };

        // Act
        var replyMessage = RabbitMqOutgoingMessages.Reply(reply, delivery, JsonOptions);

        // Assert
        replyMessage.ShouldBeNull();
    }

    private static MessageConfiguration NewConfiguration() => new(OrdersExchange);

    private static MessageEnvelope Envelope(RabbitMqOutgoingMessage message) =>
        JsonSerializer.Deserialize<MessageEnvelope>(message.Body.Span, JsonOptions).ShouldNotBeNull();

    private static Fault<OrderPlaced> NewFault(Uri? faultAddress = null) => new()
    {
        Message = new OrderPlaced("A-1"),
        ExceptionMessage = "boom",
        StackTrace = null,
        ExceptionType = typeof(InvalidOperationException).FullName!,
        FaultedAt = DateTimeOffset.UnixEpoch,
        OriginalContext = new MessageContextSnapshot { CorrelationId = "order-42", FaultAddress = faultAddress },
    };

    private static BasicDeliverEventArgs Delivery(BasicProperties properties, byte[]? body = null) =>
        new("consumer-tag", 1, false, OrdersExchange, "order.placed", properties, body ?? JsonSerializer.SerializeToUtf8Bytes(new { orderId = "A-1" }), CancellationToken.None);

    public sealed record OrderPlaced(string OrderId);

    /// <summary>
    /// The route and the properties of an outgoing message, compared as one row. The message id and the timestamp are
    /// fresh on every build, so the row records only whether they are present.
    /// </summary>
    internal sealed record WireRow(
        string Exchange,
        string RoutingKey,
        bool Mandatory,
        string? Type,
        string? ContentType,
        string? CorrelationId,
        string? ReplyTo,
        bool Persistent,
        string? Expiration,
        bool HasMessageId,
        bool HasTimestamp)
    {
        public static WireRow Of(RabbitMqOutgoingMessage message) => new(
            message.Exchange,
            message.RoutingKey,
            message.Mandatory,
            message.Properties.Type,
            message.Properties.ContentType,
            message.Properties.CorrelationId,
            message.Properties.ReplyTo,
            message.Properties.Persistent,
            message.Properties.Expiration,
            !string.IsNullOrEmpty(message.Properties.MessageId),
            message.Properties.Timestamp.UnixTime > 0);
    }
}
