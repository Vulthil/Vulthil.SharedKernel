using Vulthil.Framework.Results.Results;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Tests.Core.Results;

public sealed class MapResultExtensionsTests : MapResultBaseTestCase
{
    [Fact]
    public void MapResultSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var result2 = result.Map(FuncT2);

        // Assert
        AssertSuccess(result2);
    }

    [Fact]
    public void MapResultSuccessT1T2()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var result2 = result.Map(FuncT1T2);

        // Assert
        AssertSuccess(result2);
    }

    [Fact]
    public void MapResultFailure()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var result2 = result.Map(FailT2);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public void MapResultFailureT1T2()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var result2 = result.Map(FailT1T2);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public async Task MapAsyncResult()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.MapAsync(TaskFuncT2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task MapAsyncResultLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.MapAsync(FuncT2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task MapAsyncResultRight()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var task = result.MapAsync(TaskFuncT2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task MapAsyncResultFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.MapAsync(FailTaskT2);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task MapAsyncResultFailureLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.MapAsync(FailT2);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task MapAsyncResultFailureRight()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var task = result.MapAsync(FailTaskT2);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task MapAsyncResultSuccessT1T2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.MapAsync(TaskFuncT1T2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task MapAsyncResultSuccessT1T2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.MapAsync(FuncT1T2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task MapAsyncResultSuccessT1T2Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var task = result.MapAsync(TaskFuncT1T2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task MapAsyncResultFailureT1T2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.MapAsync(FailTaskT1T2);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task MapAsyncResultFailureT1T2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.MapAsync(FailTaskT1T2);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task MapAsyncResultFailureT1T2Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var task = result.MapAsync(FailTaskT1T2);

        // Assert
        AssertFailure(await task);
    }
}
