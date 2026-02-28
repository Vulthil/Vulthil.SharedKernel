namespace Vulthil.Results.Tests.Results;

/// <summary>
/// Represents the ToResultExtensionsTests.
/// </summary>
public sealed class ToResultExtensionsTests : ResultBaseTestCase
{
    private static void AssertSuccess(Result result)
    {
        result.IsSuccess.ShouldBeTrue();
    }

    private static void AssertFailure(Result result)
    {
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(NullError);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void ToResultStruct()
    {
        // Arrange
        int? value = 1;

        // Act
        var result = value.ToResult(NullError);

        // Assert
        AssertSuccess(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void ToResultNullStruct()
    {
        // Arrange
        int? value = null;

        // Act
        var result = value.ToResult(NullError);

        // Assert
        AssertFailure(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void ToResultObject()
    {
        // Arrange
        T1? value = T1.Value;

        // Act
        var result = value.ToResult(NullError);

        // Assert
        AssertSuccess(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void ToResultNullObject()
    {
        // Arrange
        T1? value = null;

        // Act
        var result = value.ToResult(NullError);

        // Assert
        AssertFailure(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task ToResultAsyncStruct()
    {
        // Arrange
        var value = Task.FromResult<int?>(1);

        // Act
        var result = await value.ToResultAsync(NullError);

        // Assert
        AssertSuccess(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task ToResultAsyncNullStruct()
    {
        // Arrange
        var value = Task.FromResult<int?>(null);

        // Act
        var result = await value.ToResultAsync(NullError);

        // Assert
        AssertFailure(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task ToResultAsyncObject()
    {
        // Arrange
        var value = Task.FromResult<T1?>(T1.Value);

        // Act
        var result = await value.ToResultAsync(NullError);

        // Assert
        AssertSuccess(result);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public async Task ToResultAsyncNullObject()
    {
        // Arrange
        var value = Task.FromResult<T1?>(null);

        // Act
        var result = await value.ToResultAsync(NullError);

        // Assert
        AssertFailure(result);
    }
}
