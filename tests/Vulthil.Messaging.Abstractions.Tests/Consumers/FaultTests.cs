using System.Text.Json;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Abstractions.Tests.Consumers;

public sealed class FaultTests : BaseUnitTestCase
{
    private static JsonSerializerOptions Options => new() { WriteIndented = false };

    [Fact]
    public void FaultShouldExposeSuppliedValues()
    {
        // Arrange
        var faultedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var context = CreateSnapshot();

        // Act
        var fault = new Fault<OrderPlaced>
        {
            Message = new OrderPlaced("abc", 42),
            ExceptionMessage = "boom",
            StackTrace = "   at Consumer.ConsumeAsync()",
            ExceptionType = "System.InvalidOperationException",
            FaultedAt = faultedAt,
            OriginalContext = context,
        };

        // Assert
        fault.Message.ShouldBe(new OrderPlaced("abc", 42));
        fault.ExceptionMessage.ShouldBe("boom");
        fault.StackTrace.ShouldBe("   at Consumer.ConsumeAsync()");
        fault.ExceptionType.ShouldBe("System.InvalidOperationException");
        fault.FaultedAt.ShouldBe(faultedAt);
        fault.OriginalContext.ShouldBe(context);
    }

    [Fact]
    public void FaultShouldAllowNullStackTrace()
    {
        // Arrange & Act
        var fault = CreateFullFault() with { StackTrace = null };

        // Assert
        fault.StackTrace.ShouldBeNull();
    }

    [Fact]
    public void FaultsWithSameValuesShouldBeEqual()
    {
        // Arrange
        var first = CreateFullFault();
        var second = CreateFullFault();

        // Act & Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void FaultsWithDifferentExceptionMessagesShouldNotBeEqual()
    {
        // Arrange
        var first = CreateFullFault();
        var second = CreateFullFault() with { ExceptionMessage = "different" };

        // Act & Assert
        first.ShouldNotBe(second);
    }

    [Fact]
    public void FaultsWithDifferentOriginalContextShouldNotBeEqual()
    {
        // Arrange
        var first = CreateFullFault();
        var second = CreateFullFault() with { OriginalContext = CreateSnapshot() with { RetryCount = 7 } };

        // Act & Assert
        first.ShouldNotBe(second);
    }

    [Fact]
    public void WithExpressionShouldProduceModifiedCopyWithoutMutatingOriginal()
    {
        // Arrange
        var original = CreateFullFault();

        // Act
        var modified = original with { ExceptionMessage = "changed" };

        // Assert
        modified.ExceptionMessage.ShouldBe("changed");
        original.ExceptionMessage.ShouldBe("boom");
    }

    [Fact]
    public void FaultShouldRoundtripThroughJson()
    {
        // Arrange
        var original = CreateFullFault();

        // Act
        var json = JsonSerializer.SerializeToUtf8Bytes(original, Options);
        var roundtripped = JsonSerializer.Deserialize<Fault<OrderPlaced>>(json, Options);

        // Assert
        roundtripped.ShouldNotBeNull();
        roundtripped.ShouldBe(original);
    }

    private static Fault<OrderPlaced> CreateFullFault() => new()
    {
        Message = new OrderPlaced("abc", 42),
        ExceptionMessage = "boom",
        StackTrace = "   at Consumer.ConsumeAsync()",
        ExceptionType = "System.InvalidOperationException",
        FaultedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
        OriginalContext = CreateSnapshot(),
    };

    private static MessageContextSnapshot CreateSnapshot() => new()
    {
        MessageId = "msg-1",
        CorrelationId = "corr-1",
        SourceAddress = new Uri("queue:producer"),
        RoutingKey = "order.placed",
        RetryCount = 3,
    };

    private sealed record OrderPlaced(string OrderId, int Amount);
}
