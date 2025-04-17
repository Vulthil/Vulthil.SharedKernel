using Vulthil.SharedKernel.Primitives;
using Vulthil.SharedKernel.xUnit;
using Xunit.Internal;

namespace Vulthil.SharedKernel.Tests.Core.Results;

public sealed class ResultExtensionsTests : ResultBaseTestCase
{
    [Fact]
    public void MapResultSuccess()
    {
        // Arrange
        var result = Result.Success();
        var value = 1;

        // Act
        var result2 = result.Map(() => value);

        // Assert
        AssertSuccess(result2);
    }

    [Fact]
    public void MapResultSuccessT1T2()
    {
        // Arrange
        var result = Result.Success(1);
        var value = 2;

        // Act½
        var result2 = result.Map((v) => value);

        // Assert
        AssertSuccess(result2);
    }

    [Fact]
    public void MapResultFailure()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var result2 = result.Map(Fail<int>);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public void MapResultFailureT1T2()
    {
        // Arrange
        var result = Result.Failure<int>(NullError);

        // Act
        var result2 = result.Map(Fail<int, int>);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public async Task MapAsyncResult()
    {
        // Arrange
        var result = Result.Success();
        var resultTask = Task.FromResult(result);
        var value = 1;
        var taskFromValue = Task.FromResult(value);

        // Act
        List<Task<Result<int>>> actions = [result.MapAsync(() => taskFromValue), resultTask.MapAsync(() => taskFromValue), resultTask.MapAsync(() => value)];

        // Assert
        var results = await Task.WhenAll(actions);
        results.ForEach(AssertSuccess);
    }

    [Fact]
    public async Task MapAsyncResultFailure()
    {
        // Arrange
        var result = Result.Failure(NullError);
        var resultTask = Task.FromResult(result);
        var value = 1;
        var taskFromValue = Task.FromResult(value);
        // Act
        List<Task<Result<int>>> actions = [result.MapAsync(FailTask<int>), resultTask.MapAsync(FailTask<int>), resultTask.MapAsync(Fail<int>)];

        // Assert
        var results = await Task.WhenAll(actions);
        results.ForEach(AssertFailure);
    }
}

public abstract class ResultBaseTestCase : BaseUnitTestCase
{
    protected static Error NullError { get; } = Error.NullValue;
    protected static T Fail<T>() { Assert.Fail("Should not be called"); return default; }
    protected static Task<T> FailTask<T>() { Assert.Fail("Should not be called"); return default; }
    protected static T2 Fail<T1, T2>(T1 _) { Assert.Fail("Should not be called"); return default; }
    protected static Task<T2> FailTask<T1, T2>(T1 _) { Assert.Fail("Should not be called"); return default; }

    protected static void AssertSuccess(Result output)
    {
        output.IsSuccess.ShouldBeTrue();
    }

    protected static void AssertSuccess<T>(T expected, Result<T> output)
    {
        AssertSuccess(output);
        output.Value.ShouldBe(expected);
    }

    protected static void AssertFailure(Result output)
    {
        output.IsFailure.ShouldBeTrue();
        output.Error.ShouldBe(NullError);
    }
}
