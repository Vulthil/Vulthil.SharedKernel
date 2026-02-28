using Vulthil.Messaging.RabbitMq;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Represents the RabbitMqConstantsTests.
/// </summary>
public sealed class RabbitMqConstantsTests : BaseUnitTestCase
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void ContentTypeShouldBeApplicationJson()
    {
        // Arrange & Act & Assert
        RabbitMqConstants.ContentType.ShouldBe("application/json");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void GetMetadataShouldReturnPickerResultWhenTypeExists()
    {
        // Arrange
        var registry = new Dictionary<Type, Func<object, string>>
        {
            { typeof(TestMessage), msg => "test-value" }
        };
        var message = new TestMessage();

        // Act
        var result = RabbitMqConstants.GetMetadata(typeof(TestMessage), message, registry);

        // Assert
        result.ShouldBe("test-value");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void GetMetadataShouldReturnNullWhenTypeNotFound()
    {
        // Arrange
        var registry = new Dictionary<Type, Func<object, string>>();
        var message = new TestMessage();

        // Act
        var result = RabbitMqConstants.GetMetadata(typeof(TestMessage), message, registry);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void GetMetadataShouldWalkInheritanceTree()
    {
        // Arrange
        var registry = new Dictionary<Type, Func<object, string>>
        {
            { typeof(BaseMessage), msg => "base-value" }
        };
        var message = new DerivedMessage();

        // Act
        var result = RabbitMqConstants.GetMetadata(typeof(DerivedMessage), message, registry);

        // Assert
        result.ShouldBe("base-value");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void GetMetadataShouldPreferDerivedTypeOverBase()
    {
        // Arrange
        var registry = new Dictionary<Type, Func<object, string>>
        {
            { typeof(BaseMessage), msg => "base-value" },
            { typeof(DerivedMessage), msg => "derived-value" }
        };
        var message = new DerivedMessage();

        // Act
        var result = RabbitMqConstants.GetMetadata(typeof(DerivedMessage), message, registry);

        // Assert
        result.ShouldBe("derived-value");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void GetMetadataShouldReturnNullForObjectType()
    {
        // Arrange
        var registry = new Dictionary<Type, Func<object, string>>();

        // Act
        var result = RabbitMqConstants.GetMetadata(typeof(object), new object(), registry);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void GetMetadataShouldHandleMultipleLevelsOfInheritance()
    {
        // Arrange
        var registry = new Dictionary<Type, Func<object, string>>
        {
            { typeof(Level0Message), msg => "level0" }
        };
        var message = new Level2Message();

        // Act
        var result = RabbitMqConstants.GetMetadata(typeof(Level2Message), message, registry);

        // Assert
        result.ShouldBe("level0");
    }

    private class TestMessage { }
    private class BaseMessage { }
    private class DerivedMessage : BaseMessage { }
    private class Level0Message { }
    private class Level1Message : Level0Message { }
    private class Level2Message : Level1Message { }
}
