using Vulthil.Results;

namespace Vulthil.Results.Tests.Results;

/// <summary>
/// Represents the BindResultBaseTestCase.
/// </summary>
public abstract class BindResultBaseTestCase : ResultBaseTestCase
{

    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected T1? Param { get; private set; }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result Success()
    {
        FuncExecuted = true;
        return Result.Success();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result SuccessT1(T1 _)
    {
        Param = _;
        return Success();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result<T1> SuccessT1()
    {
        FuncExecuted = true;
        return Result.Success(T1.Value);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result<T2> SuccessT2()
    {
        FuncExecuted = true;
        return Result.Success(T2.Value);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result<T2> SuccessT1T2(T1 _)
    {
        Param = _;
        return SuccessT2();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result> TaskSuccess()
    {
        return Task.FromResult(Success());
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result> TaskSuccessT1(T1 _)
    {
        return Task.FromResult(SuccessT1(_));
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result<T1>> TaskSuccessT1()
    {
        return Task.FromResult(SuccessT1());
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result<T2>> TaskSuccessT2()
    {
        return Task.FromResult(SuccessT2());
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result<T2>> TaskSuccessT1T2(T1 _)
    {
        return Task.FromResult(SuccessT1T2(_));
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result Failure()
    {
        FuncExecuted = false;
        return Result.Failure(NullError);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result<T1> FailureT1()
    {
        FuncExecuted = false;
        return Result.Failure<T1>(NullError);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result FailureT1(T1 _)
    {
        Param = _;
        return Failure();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result<T2> FailureT2()
    {
        FuncExecuted = false;
        return Result.Failure<T2>(NullError);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Result<T2> FailureT1T2(T1 _)
    {
        Param = _;
        return FailureT2();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result> TaskFailure()
    {
        return Task.FromResult(Failure());
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result<T1>> TaskFailureT1()
    {
        return Task.FromResult(FailureT1());
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result<T2>> TaskFailureT2()
    {
        return Task.FromResult(FailureT2());
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<Result<T2>> TaskFailureT1T2(T1 _)
    {
        return Task.FromResult(FailureT1T2(_));
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccess(Result output) => BaseAssertSuccess(output);
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertFailure(Result output) => BaseAssertFailure(output);
}

/// <summary>
/// Represents the MatchResultBaseTestCase.
/// </summary>
public abstract class MatchResultBaseTestCase : ResultBaseTestCase
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected bool SuccessExecuted { get; private set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected bool FailureExecuted { get; private set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected T1? Param { get; private set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected Error? Error { get; private set; }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccess()
    {
        SuccessExecuted.ShouldBeTrue();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccess(Result output)
    {
        AssertSuccess();
        BaseAssertSuccess(output);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccessT1()
    {
        AssertSuccess();
        Param.ShouldBe(T1.Value);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccessT2(T2 output)
    {
        AssertSuccess();
        output.ShouldBe(T2.Value);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccessT1T2(T2 output)
    {
        AssertSuccessT1();
        AssertSuccessT2(output);
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertFailure()
    {
        FailureExecuted.ShouldBeTrue();
        Error.ShouldBe(NullError);
        Param.ShouldBeNull();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertFailureT2(T2 output)
    {
        AssertFailure();
        output.ShouldBe(T2.Value2);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void OnSuccess()
    {
        FuncExecuted = true;
        SuccessExecuted = true;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void OnSuccessT1(T1 _)
    {
        OnSuccess();
        Param = _;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected T2 OnSuccessT2()
    {
        OnSuccess();
        return T2.Value;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected T2 OnSuccessT1T2(T1 _)
    {
        OnSuccessT1(_);
        return OnSuccessT2();
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task OnSuccessTask()
    {
        OnSuccess();
        return Task.CompletedTask;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task OnSuccessTaskT1(T1 _)
    {
        OnSuccessT1(_);
        return Task.CompletedTask;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<T2> OnSuccessTaskT2() => Task.FromResult(OnSuccessT2());
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<T2> OnSuccessTaskT1T2(T1 _) => Task.FromResult(OnSuccessT1T2(_));

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void OnFailure(Error _)
    {
        FailureExecuted = true;
        Error = _;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected T2 OnFailureT2(Error _)
    {
        OnFailure(_);
        return T2.Value2;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task OnFailureTask(Error _)
    {
        OnFailure(_);
        return Task.CompletedTask;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<T2> OnFailureTaskT2(Error _) => Task.FromResult(OnFailureT2(_));
}
