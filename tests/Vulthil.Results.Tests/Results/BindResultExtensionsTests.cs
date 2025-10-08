namespace Vulthil.Results.Tests.Results;

public sealed class BindResultExtensionsTests : BindResultBaseTestCase
{
    [Fact]
    public void BindResultSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var result2 = result.Bind(Success);

        // Assert
        AssertSuccess(result2);
    }

    [Fact]
    public void BindResultFailure()
    {
        // Arrange
        var result = Failure();

        // Act
        var result2 = result.Bind(Success);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public void BindResultSuccessT1()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var result2 = result.Bind((_) => SuccessT1(_));

        // Assert
        AssertSuccess(result2);
        Param.ShouldBe(result.Value);
    }

    [Fact]
    public void BindResultFailureT1()
    {
        // Arrange
        var result = FailureT1();

        // Act
        var result2 = result.Bind(FailResultT1);

        // Assert
        AssertFailure(result2);
        Param.ShouldBeNull();
    }

    [Fact]
    public void BindResultSuccessT2()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var result2 = result.Bind(SuccessT2);

        // Assert
        AssertSuccess(result2);
        result2.Value.ShouldBe(T2.Value);
    }

    [Fact]
    public void BindResultFailureT2()
    {
        // Arrange
        var result = Failure();

        // Act
        var result2 = result.Bind(FailResultT2);

        // Assert
        AssertFailure(result2);
    }

    [Fact]
    public void BindResultSuccessT1T2()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var result2 = result.Bind(SuccessT1T2);

        // Assert
        AssertSuccess(result2);
        Param.ShouldBe(result.Value);
        result2.Value.ShouldBe(T2.Value);
    }

    [Fact]
    public void BindResultFailureT1T2()
    {
        // Arrange
        var result = FailureT1();

        // Act
        var result2 = result.Bind(FailResultT1T2);

        // Assert
        AssertFailure(result2);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResult()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.BindAsync(TaskSuccess);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task BindAsyncResultLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.BindAsync(Success);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task BindAsyncResultRight()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var task = result.BindAsync(TaskSuccess);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task BindAsyncResultFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.BindAsync(TaskSuccess);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task BindAsyncResultFailureLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.BindAsync(Success);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task BindAsyncResultFailureRight()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var task = result.BindAsync(TaskSuccess);

        // Assert
        AssertFailure(await task);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.BindAsync(_ => TaskSuccessT1(_));

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.BindAsync(_ => SuccessT1(_));

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT1Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var task = result.BindAsync(_ => TaskSuccessT1(_));

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task BindAsyncResultFailureT1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.BindAsync(FailResultTaskT1);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultFailureT1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.BindAsync(FailResultT1);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultFailureT1Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var task = result.BindAsync(FailResultTaskT1);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultSuccessT2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.BindAsync(TaskSuccessT2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var task = resultTask.BindAsync(SuccessT2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT2Right()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var task = result.BindAsync(TaskSuccessT2);

        // Assert
        AssertSuccess(await task);
    }

    [Fact]
    public async Task BindAsyncResultFailureT2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.BindAsync(FailResultTaskT2);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultFailureT2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var task = resultTask.BindAsync(FailResultT2);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultFailureT2Right()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var task = result.BindAsync(FailResultTaskT2);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultSuccessT1T2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.BindAsync(TaskSuccessT1T2);

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT1T2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var task = resultTask.BindAsync(SuccessT1T2);

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task BindAsyncResultSuccessT1T2Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var task = result.BindAsync(TaskSuccessT1T2);

        // Assert
        AssertSuccess(await task);
        Param.ShouldBe(T1.Value);
    }

    [Fact]
    public async Task BindAsyncResultFailureT1T2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.BindAsync(FailResultTaskT1T2);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultFailureT1T2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var task = resultTask.BindAsync(FailResultT1T2);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsyncResultFailureT1T2Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var task = result.BindAsync(FailResultTaskT1T2);

        // Assert
        AssertFailure(await task);
        Param.ShouldBeNull();
    }
}
