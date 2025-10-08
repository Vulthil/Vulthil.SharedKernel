using System.Diagnostics.CodeAnalysis;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Results.Tests.Results;

public abstract class ResultBaseTestCase : BaseUnitTestCase
{
    protected bool FuncExecuted { get; set; }
    protected static Error NullError { get; } = Error.NullValue;

    protected sealed class T1
    {
        public static readonly T1 Value = new();

        public static readonly T1 Value2 = new();
    }
    protected sealed class T2
    {
        public static readonly T2 Value = new();

        public static readonly T2 Value2 = new();
    }

    [DoesNotReturn]
    protected static void Fail() => Assert.Fail("Should not be called");
    protected static void Fail(Error _) => Fail();
    protected static Task FailTask() { Fail(); return default; }
    protected static Task FailTask(Error _) => FailTask();
    protected static Result FailResult() { Fail(); return default; }
    protected static Result FailResult(Error _) => FailResult();
    protected static Task<Result> FailResultTask() => Task.FromResult(FailResult());
    protected static Task<Result> FailResultTask(Error _) => FailResultTask();

    protected static void FailT1(T1 _) => Fail();
    protected static Task FailTaskT1(T1 _) { Fail(); return default; }
    protected static Result FailResultT1(T1 _) { Fail(); return default; }
    protected static Task<Result> FailResultTaskT1(T1 _) => Task.FromResult(FailResultT1(_));

    protected static T2 FailT2() { Fail(); return default; }
    protected static T2 FailT2(Error _) => FailT2();
    protected static Task<T2> FailTaskT2() => Task.FromResult(FailT2());
    protected static Task<T2> FailTaskT2(Error _) => FailTaskT2();
    protected static Result<T2> FailResultT2() => Result.Success(FailT2());
    protected static Result<T2> FailResultT2(Error _) => FailResultT2();
    protected static Task<Result<T2>> FailResultTaskT2() => Task.FromResult(FailResultT2());
    protected static Task<Result<T2>> FailResultTaskT2(Error _) => FailResultTaskT2();

    protected static T2 FailT1T2(T1 _) => FailT2();
    protected static Task<T2> FailTaskT1T2(T1 _) => FailTaskT2();
    protected static Result<T2> FailResultT1T2(T1 _) => FailResultT2();
    protected static Task<Result<T2>> FailResultTaskT1T2(T1 _) => FailResultTaskT2();



    protected void BaseAssertSuccess(Result output)
    {
        FuncExecuted.ShouldBeTrue();
        output.IsSuccess.ShouldBeTrue();
    }

    protected void BaseAssertSuccess<T>(T expected, Result<T> output)
    {
        BaseAssertSuccess(output);
        output.Value.ShouldBe(expected);
    }

    protected void BaseAssertFailure(Result output)
    {
        FuncExecuted.ShouldBeFalse();
        output.IsFailure.ShouldBeTrue();
        output.Error.ShouldBe(NullError);
    }
}
