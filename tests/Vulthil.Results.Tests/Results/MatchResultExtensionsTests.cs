namespace Vulthil.Results.Tests.Results;

public sealed class MatchResultExtensionsTests : MatchResultBaseTestCase
{
    [Fact]
    public void MatchResultSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        result.Match(OnSuccess, Fail);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public void MatchResultFailure()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        result.Match(Fail, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public void MatchResultSuccessT1()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        result.Match(OnSuccessT1, Fail);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public void MatchResultFailureT1()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        result.Match(FailT1, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public void MatchResultSuccessT2()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var t2 = result.Match(OnSuccessT2, FailT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public void MatchResultFailureT2()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var t2 = result.Match(FailT2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public void MatchResultSuccessT1T2()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var t2 = result.Match(OnSuccessT1T2, OnFailureT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public void MatchResultFailureT1T2()
    {
        // Arrange

        var result = Result.Failure<T1>(NullError);

        // Act
        var t2 = result.Match(FailT1T2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResult()
    {
        // Arrange
        var result = Result.Success();

        // Act
        await result.MatchAsync(OnSuccessTask, FailTask);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultLeft()
    {
        // Arrange
        var result = Result.Success();

        // Act
        await result.MatchAsync(OnSuccessTask, Fail);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultRight()
    {
        // Arrange
        var result = Result.Success();

        // Act
        await result.MatchAsync(OnSuccess, FailTask);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultTask1()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        await resultTask.MatchAsync(OnSuccess, Fail);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultTask2()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        await resultTask.MatchAsync(OnSuccessTask, FailTask);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultTaskLeft()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        await resultTask.MatchAsync(OnSuccessTask, Fail);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultTaskRight()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        await resultTask.MatchAsync(OnSuccess, FailTask);

        // Assert
        AssertSuccess();
    }

    [Fact]
    public async Task MatchAsyncResultFailure()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        await result.MatchAsync(FailTask, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultFailureLeft()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        await result.MatchAsync(FailTask, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultFailureRight()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        await result.MatchAsync(Fail, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailure1()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure(NullError));

        // Act
        await result.MatchAsync(Fail, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailure2()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure(NullError));

        // Act
        await result.MatchAsync(FailTask, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureLeft()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure(NullError));

        // Act
        await result.MatchAsync(FailTask, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureRight()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure(NullError));

        // Act
        await result.MatchAsync(Fail, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultT1()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        await result.MatchAsync(OnSuccessTaskT1, FailTask);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultT1Left()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        await result.MatchAsync(OnSuccessTaskT1, Fail);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultT1Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        await result.MatchAsync(OnSuccessT1, FailTask);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultTaskT11()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        await resultTask.MatchAsync(OnSuccessT1, Fail);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultTaskT12()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        await resultTask.MatchAsync(OnSuccessTaskT1, FailTask);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultTaskT1Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        await resultTask.MatchAsync(OnSuccessTaskT1, Fail);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultTaskT1Right()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        await resultTask.MatchAsync(OnSuccessT1, FailTask);

        // Assert
        AssertSuccessT1();
    }

    [Fact]
    public async Task MatchAsyncResultFailureT1()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        await result.MatchAsync(FailTaskT1, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultFailureT1Left()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        await result.MatchAsync(FailTaskT1, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultFailureT1Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        await result.MatchAsync(FailT1, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT11()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        await result.MatchAsync(FailT1, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT12()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        await result.MatchAsync(FailTaskT1, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT1Left()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        await result.MatchAsync(FailTaskT1, OnFailure);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT1Right()
    {
        // Arrange
        var result = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        await result.MatchAsync(FailT1, OnFailureTask);

        // Assert
        AssertFailure();
    }

    [Fact]
    public async Task MatchAsyncResultT2()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var t2 = await result.MatchAsync(OnSuccessTaskT2, FailTaskT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultT2Left()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var t2 = await result.MatchAsync(OnSuccessTaskT2, FailT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultT2Right()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var t2 = await result.MatchAsync(OnSuccessT2, FailTaskT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT21()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessT2, FailT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT22()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessTaskT2, FailTaskT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessTaskT2, FailT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT2Right()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessT2, FailTaskT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultFailureT2()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var t2 = await result.MatchAsync(FailTaskT2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultFailureT2Left()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var t2 = await result.MatchAsync(FailTaskT2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultFailureT2Right()
    {
        // Arrange
        var result = Result.Failure(NullError);

        // Act
        var t2 = await result.MatchAsync(FailT2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT21()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailT2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT22()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailTaskT2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailTaskT2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT2Right()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailT2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultT1T2()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var t2 = await result.MatchAsync(OnSuccessTaskT1T2, FailTaskT2);

        // Assert
        AssertSuccessT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultT1T2Left()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var t2 = await result.MatchAsync(OnSuccessTaskT1T2, FailT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultT1T2Right()
    {
        // Arrange
        var result = Result.Success(T1.Value);

        // Act
        var t2 = await result.MatchAsync(OnSuccessT1T2, FailTaskT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT1T21()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessT1T2, FailT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT1T22()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessTaskT1T2, FailTaskT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT1T2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessTaskT1T2, FailT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskT1T2Right()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(T1.Value));

        // Act
        var t2 = await resultTask.MatchAsync(OnSuccessT1T2, FailTaskT2);

        // Assert
        AssertSuccessT1T2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultFailureT1T2()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var t2 = await result.MatchAsync(FailTaskT1T2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultFailureT1T2Left()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var t2 = await result.MatchAsync(FailTaskT1T2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultFailureT1T2Right()
    {
        // Arrange
        var result = Result.Failure<T1>(NullError);

        // Act
        var t2 = await result.MatchAsync(FailT1T2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT1T21()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailT1T2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT1T22()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailTaskT1T2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT1T2Left()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailTaskT1T2, OnFailureT2);

        // Assert
        AssertFailureT2(t2);
    }

    [Fact]
    public async Task MatchAsyncResultTaskFailureT1T2Right()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<T1>(NullError));

        // Act
        var t2 = await resultTask.MatchAsync(FailT1T2, OnFailureTaskT2);

        // Assert
        AssertFailureT2(t2);
    }
}
