using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Tests.Core.Results;

public sealed class TapResultExtensionsTests : TapResultBaseTestCase
{
    [Fact]
    public void TapResultSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var result2 = result.Tap(Func);

        // Assert
        AssertSuccess(result2);
    }

    [Fact]
    public void TapResultSuccessT1()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var result2 = result.Tap(Func);

        // Assert
        AssertSuccess(result2);
        result.ShouldBe(result);
    }

    [Fact]
    public void TapResultSuccessT1T1()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var result2 = result.Tap(FuncT1);

        // Assert
        AssertSuccess(result2);
        result.ShouldBe(result);
        Param.ShouldBe(result.Value);
    }

    [Fact]
    public void TapResultFailure()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var result2 = result.Tap(Fail);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public void TapResultFailureT1()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var result2 = result.Tap(Fail);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public void TapResultFailureT1T1()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var result2 = result.Tap(FailT1);

        // Assert
        AssertFailure(result2);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task TapAsyncResult()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.TapAsync(TaskFunc);

        // Assert
        AssertSuccess(await task);

    }

    [Fact]
    public async Task TapAsyncResultLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.TapAsync(Func);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task TapAsyncResultRight()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var task = result.TapAsync(TaskFunc);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task TapAsyncResultFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.TapAsync(FailTask);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task TapAsyncResultFailureLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.TapAsync(Fail);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task TapAsyncResultFailureRight()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var task = result.TapAsync(FailTask);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task TapAsyncResultSuccessT1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.TapAsync(TaskFunc);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task TapAsyncResultSuccessT1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.TapAsync(Func);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task TapAsyncResultSuccessT1Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var task = result.TapAsync(TaskFunc);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task TapAsyncResultSuccessT1T1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.TapAsync(TaskFuncT1);

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task TapAsyncResultSuccessT1T1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.TapAsync(FuncT1);

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task TapAsyncResultSuccessT1T1Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var task = result.TapAsync(TaskFuncT1);

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task TapAsyncResultFailureT1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.TapAsync(FailTask);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task TapAsyncResultFailureT1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.TapAsync(Fail);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task TapAsyncResultFailureT1Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var task = result.TapAsync(FailTask);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task TapAsyncResultFailureT1T1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.TapAsync(FailTaskT1);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task TapAsyncResultFailureT1T1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.TapAsync(FailT1);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task TapAsyncResultFailureT1T1Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var task = result.TapAsync(FailTaskT1);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }
}
