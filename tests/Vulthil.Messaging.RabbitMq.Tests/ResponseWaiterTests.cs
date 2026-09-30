using System.Text.Json;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.Transport;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class ResponseWaiterTests : BaseUnitTestCase
{
    private readonly Lazy<ResponseWaiter<PricingReply>> _lazyTarget;
    private readonly TaskCompletionSource<Result<PricingReply>> _completion = new();
    private readonly IMessageConfigurationProvider _provider = TestProviders.Build();

    private ResponseWaiter<PricingReply> Target => _lazyTarget.Value;

    public ResponseWaiterTests()
    {
        Use(_completion);
        Use(_provider);
        _lazyTarget = new(CreateInstance<ResponseWaiter<PricingReply>>);
    }

    private byte[] Reply(Uri messageType, object message)
        => JsonSerializer.SerializeToUtf8Bytes(
            new MessageEnvelope
            {
                MessageType = messageType,
                Message = JsonSerializer.SerializeToElement(message, _provider.JsonSerializerOptions),
            },
            _provider.JsonSerializerOptions);

    [Fact]
    public async Task CompleteResolvesTheReplyAsASuccessWhenItCarriesTheResponseUrn()
    {
        // Arrange
        var reply = new PricingReply("sku-1", 9.5m);

        // Act
        Target.Complete(Reply(_provider.GetUrn(typeof(PricingReply)), reply));
        var result = await _completion.Task;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(reply);
    }

    [Fact]
    public async Task CompleteResolvesANullEnvelopeAsAFailure()
    {
        // Act
        Target.Complete("null"u8);
        var result = await _completion.Task;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Deserialize");
        result.Error.Description.ShouldBe("Reply envelope was null.");
    }

    [Fact]
    public async Task CompleteResolvesMalformedBytesAsAFailureInsteadOfThrowing()
    {
        // Act
        Target.Complete("{ not json"u8);
        var result = await _completion.Task;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Deserialize");
        result.Error.Description.ShouldStartWith("Deserialization error");
    }

    public sealed record PricingReply(string Sku, decimal Price);
}
