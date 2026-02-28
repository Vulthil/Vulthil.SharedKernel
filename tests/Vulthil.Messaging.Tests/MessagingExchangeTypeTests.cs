using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

/// <summary>
/// Represents the MessagingExchangeTypeTests.
/// </summary>
public sealed class MessagingExchangeTypeTests : BaseUnitTestCase
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void FanoutExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Fanout.ShouldBe(MessagingExchangeType.Fanout);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void DirectExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Direct.ShouldBe(MessagingExchangeType.Direct);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void TopicExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Topic.ShouldBe(MessagingExchangeType.Topic);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void HeadersExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Headers.ShouldBe(MessagingExchangeType.Headers);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void AllExchangeTypesAreUnique()
    {
        // Arrange & Act
        var types = new[]
        {
            MessagingExchangeType.Fanout,
            MessagingExchangeType.Direct,
            MessagingExchangeType.Topic,
            MessagingExchangeType.Headers
        };

        // Assert
        types.Length.ShouldBe(4);
        types.Distinct().Count().ShouldBe(4);
    }
}
