using Vulthil.Results;

namespace Vulthil.SharedKernel.Tests.Core.Results;

public abstract class BindResultBaseTestCase : ResultBaseTestCase
{

    protected T1? Param { get; private set; }

    protected Result Success()
    {
        FuncExecuted = true;
        return Result.Success();
    }
    protected Result SuccessT1(T1 _)
    {
        Param = _;
        return Success();
    }
    protected Result<T1> SuccessT1()
    {
        FuncExecuted = true;
        return Result.Success(T1.Value);
    }
    protected Result<T2> SuccessT2()
    {
        FuncExecuted = true;
        return Result.Success(T2.Value);
    }
    protected Result<T2> SuccessT1T2(T1 _)
    {
        Param = _;
        return SuccessT2();
    }

    protected Task<Result> TaskSuccess()
    {
        return Task.FromResult(Success());
    }
    protected Task<Result> TaskSuccessT1(T1 _)
    {
        return Task.FromResult(SuccessT1(_));
    }
    protected Task<Result<T1>> TaskSuccessT1()
    {
        return Task.FromResult(SuccessT1());
    }
    protected Task<Result<T2>> TaskSuccessT2()
    {
        return Task.FromResult(SuccessT2());
    }
    protected Task<Result<T2>> TaskSuccessT1T2(T1 _)
    {
        return Task.FromResult(SuccessT1T2(_));
    }

    protected Result Failure()
    {
        FuncExecuted = false;
        return Result.Failure(NullError);
    }
    protected Result<T1> FailureT1()
    {
        FuncExecuted = false;
        return Result.Failure<T1>(NullError);
    }
    protected Result FailureT1(T1 _)
    {
        Param = _;
        return Failure();
    }
    protected Result<T2> FailureT2()
    {
        FuncExecuted = false;
        return Result.Failure<T2>(NullError);
    }
    protected Result<T2> FailureT1T2(T1 _)
    {
        Param = _;
        return FailureT2();
    }
    protected Task<Result> TaskFailure()
    {
        return Task.FromResult(Failure());
    }
    protected Task<Result<T1>> TaskFailureT1()
    {
        return Task.FromResult(FailureT1());
    }
    protected Task<Result<T2>> TaskFailureT2()
    {
        return Task.FromResult(FailureT2());
    }
    protected Task<Result<T2>> TaskFailureT1T2(T1 _)
    {
        return Task.FromResult(FailureT1T2(_));
    }

    protected void AssertSuccess(Result output) => BaseAssertSuccess(output);
    protected void AssertFailure(Result output) => BaseAssertFailure(output);
}

public abstract class MatchResultBaseTestCase : ResultBaseTestCase
{
    protected bool SuccessExecuted { get; private set; }
    protected bool FailureExecuted { get; private set; }
    protected T1? Param { get; private set; }
    protected Error? Error { get; private set; }

    protected void AssertSuccess()
    {
        SuccessExecuted.ShouldBeTrue();
    }
    protected void AssertSuccess(Result output)
    {
        AssertSuccess();
        BaseAssertSuccess(output);
    }
    protected void AssertSuccessT1()
    {
        AssertSuccess();
        Param.ShouldBe(T1.Value);
    }
    protected void AssertSuccessT2(T2 output)
    {
        AssertSuccess();
        output.ShouldBe(T2.Value);
    }
    protected void AssertSuccessT1T2(T2 output)
    {
        AssertSuccessT1();
        AssertSuccessT2(output);
    }
    protected void AssertFailure()
    {
        FailureExecuted.ShouldBeTrue();
        Error.ShouldBe(NullError);
        Param.ShouldBeNull();
    }
    protected void AssertFailureT2(T2 output)
    {
        AssertFailure();
        output.ShouldBe(T2.Value2);
    }

    protected void OnSuccess()
    {
        FuncExecuted = true;
        SuccessExecuted = true;
    }
    protected void OnSuccessT1(T1 _)
    {
        OnSuccess();
        Param = _;
    }
    protected T2 OnSuccessT2()
    {
        OnSuccess();
        return T2.Value;
    }
    protected T2 OnSuccessT1T2(T1 _)
    {
        OnSuccessT1(_);
        return OnSuccessT2();
    }
    protected Task OnSuccessTask()
    {
        OnSuccess();
        return Task.CompletedTask;
    }
    protected Task OnSuccessTaskT1(T1 _)
    {
        OnSuccessT1(_);
        return Task.CompletedTask;
    }
    protected Task<T2> OnSuccessTaskT2() => Task.FromResult(OnSuccessT2());
    protected Task<T2> OnSuccessTaskT1T2(T1 _) => Task.FromResult(OnSuccessT1T2(_));

    protected void OnFailure(Error _)
    {
        FailureExecuted = true;
        Error = _;
    }
    protected T2 OnFailureT2(Error _)
    {
        OnFailure(_);
        return T2.Value2;
    }
    protected Task OnFailureTask(Error _)
    {
        OnFailure(_);
        return Task.CompletedTask;
    }
    protected Task<T2> OnFailureTaskT2(Error _) => Task.FromResult(OnFailureT2(_));
}
