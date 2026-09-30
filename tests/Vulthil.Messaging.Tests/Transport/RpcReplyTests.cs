using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class RpcReplyTests : BaseUnitTestCase
{
    private const string RequestId = "request-1";
    private const string CorrelationId = "order-42";

    private readonly MessagingOptions _provider = new();

    [Fact]
    public void ASuccessReplyCarriesTheResponseAtItsUrnAndReadsBackAsTheResponse()
    {
        // Arrange
        var response = new PriceQuote("sku-1", 9.5m);
        var reply = RpcReply.Success(response, _provider, RequestId, CorrelationId);

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        reply.MessageType.ShouldBe(_provider.GetUrn(typeof(PriceQuote)));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(response);
    }

    [Fact]
    public void AFaultReplyCarriesTheExceptionAndReadsBackAsAFailureWithTheRemoteMessage()
    {
        // Arrange
        var exception = Should.Throw<InvalidOperationException>(static () => throw new InvalidOperationException("pricing unavailable"));
        var reply = RpcReply.Fault(exception, _provider, RequestId, CorrelationId);

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        reply.MessageType.ShouldBe(RpcFault.UrnUri);
        var fault = reply.Message.Deserialize<RpcFault>(_provider.JsonSerializerOptions).ShouldNotBeNull();
        fault.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        fault.StackTrace.ShouldNotBeNullOrEmpty();
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Failure");
        result.Error.Description.ShouldBe("pricing unavailable");
    }

    [Fact]
    public void AShortCircuitedReplyReadsBackAsAFailureThatNamesTheShortCircuit()
    {
        // Arrange
        var reply = RpcReply.ShortCircuited(_provider, RequestId, CorrelationId);

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        reply.MessageType.ShouldBe(RpcFault.UrnUri);
        result.Error.Code.ShouldBe("Messaging.Request.Failure");
        result.Error.Description.ShouldContain("did not produce a response");
    }

    [Fact]
    public void EveryKindOfReplyEchoesTheRequestIdAndTheCorrelationIdUnderAFreshMessageId()
    {
        // Act
        MessageEnvelope[] replies =
        [
            RpcReply.Success(new PriceQuote("sku-1", 9.5m), _provider, RequestId, CorrelationId),
            RpcReply.Fault(new InvalidOperationException("boom"), _provider, RequestId, CorrelationId),
            RpcReply.ShortCircuited(_provider, RequestId, CorrelationId),
        ];

        // Assert
        replies.ShouldAllBe(reply => reply.RequestId == RequestId && reply.CorrelationId == CorrelationId);
        replies.ShouldAllBe(reply => reply.MessageId != null && reply.SentTime != null);
        replies.Select(reply => reply.MessageId).Distinct().Count().ShouldBe(replies.Length);
    }

    [Fact]
    public void AReplyOfAnotherMessageTypeReadsBackAsADeserializeFailure()
    {
        // Arrange
        var reply = RpcReply.Success(new OtherReply(1), _provider, RequestId, CorrelationId);

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        result.Error.Code.ShouldBe("Messaging.Request.Deserialize");
        result.Error.Description.ShouldContain(reply.MessageType.ToString());
    }

    [Fact]
    public void AReplyWithANullPayloadReadsBackAsADeserializeFailure()
    {
        // Arrange
        var reply = new MessageEnvelope
        {
            MessageType = _provider.GetUrn(typeof(PriceQuote)),
            Message = JsonSerializer.SerializeToElement<PriceQuote?>(null),
        };

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        result.Error.Code.ShouldBe("Messaging.Request.Deserialize");
        result.Error.Description.ShouldBe("Inner message deserialization failed.");
    }

    [Fact]
    public void AResponsePayloadThatCannotBeReadReadsBackAsADeserializeFailureInsteadOfThrowing()
    {
        // Arrange
        var reply = new MessageEnvelope
        {
            MessageType = _provider.GetUrn(typeof(PriceQuote)),
            Message = JsonSerializer.SerializeToElement("not a quote"),
        };

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        result.Error.Code.ShouldBe("Messaging.Request.Deserialize");
        result.Error.Description.ShouldStartWith("Deserialization error");
    }

    [Fact]
    public void AFaultPayloadThatCannotBeReadReadsBackAsADeserializeFailureInsteadOfThrowing()
    {
        // Arrange
        var reply = new MessageEnvelope
        {
            MessageType = RpcFault.UrnUri,
            Message = JsonSerializer.SerializeToElement(new { unexpected = true }),
        };

        // Act
        var result = RpcReply.ToResult<PriceQuote>(reply, _provider);

        // Assert
        result.Error.Code.ShouldBe("Messaging.Request.Deserialize");
        result.Error.Description.ShouldStartWith("Deserialization error");
    }

    public sealed record PriceQuote(string Sku, decimal Price);

    public sealed record OtherReply(int Value);
}
