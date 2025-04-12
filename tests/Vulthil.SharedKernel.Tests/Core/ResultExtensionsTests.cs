using Vulthil.SharedKernel.Primitives;
using Vulthil.SharedKernel.xUnit;
using Xunit.Internal;

namespace Vulthil.SharedKernel.Tests.Core;

public sealed class ResultExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void MapResult()
    {
        // Arrange
        var result = Result.Success();
        var value = 1;

        // Act
        var result2 = result.Map(() => value);

        // Assert
        Assert.True(result2.IsSuccess);
        Assert.Equal(value, result2.Value);
    }

    [Fact]
    public void MapResultFailure()
    {
        // Arrange
        var result = Result.Failure(Error.NullValue);
        var value = 1;

        // Act
        var result2 = result.Map(() => value);

        // Assert
        Assert.False(result2.IsSuccess);
        Assert.Equal(Error.NullValue, result2.Error);
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
        results.ForEach(result => result.ShouldSatisfyAllConditions(() => result.IsSuccess.ShouldBeTrue(), () => result.Value.ShouldBe(value)));
    }

    [Fact]
    public async Task MapAsyncResultFailure()
    {
        // Arrange
        var error = Error.NullValue;
        var result = Result.Failure(error);
        var resultTask = Task.FromResult(result);
        var value = 1;
        var taskFromValue = Task.FromResult(value);
        // Act
        List<Task<Result<int>>> actions = [result.MapAsync(() => taskFromValue), resultTask.MapAsync(() => taskFromValue), resultTask.MapAsync(() => value)];

        // Assert
        var results = await Task.WhenAll(actions);
        results.ForEach(result => result.ShouldSatisfyAllConditions(() => result.IsSuccess.ShouldBeFalse(), () => result.Error.ShouldBe(error)));
    }
}
