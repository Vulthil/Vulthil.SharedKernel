using System.Diagnostics.CodeAnalysis;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Results.Tests.Results;

/// <summary>
/// Represents the ResultBaseTestCase.
/// </summary>
public abstract class ResultBaseTestCase : BaseUnitTestCase
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected bool FuncExecuted { get; set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected static Error NullError { get; } = Error.NullValue;

    /// <summary>
    /// Represents the T1.
    /// </summary>
    protected sealed class T1
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public static readonly T1 Value = new();

        /// <summary>
        /// Executes this member.
        /// </summary>
        public static readonly T1 Value2 = new();
    }
    /// <summary>
    /// Represents the T2.
    /// </summary>
    protected sealed class T2
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public static readonly T2 Value = new();

        /// <summary>
        /// Executes this member.
        /// </summary>
        public static readonly T2 Value2 = new();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [DoesNotReturn]
    protected static void Fail() => Assert.Fail("Should not be called");
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static void Fail(Error _) => Fail();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task FailTask() { Fail(); return default; }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task FailTask(Error _) => FailTask();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Result FailResult() { Fail(); return default; }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Result FailResult(Error _) => FailResult();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<Result> FailResultTask() => Task.FromResult(FailResult());
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<Result> FailResultTask(Error _) => FailResultTask();

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static void FailT1(T1 _) => Fail();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task FailTaskT1(T1 _) { Fail(); return default; }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Result FailResultT1(T1 _) { Fail(); return default; }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<Result> FailResultTaskT1(T1 _) => Task.FromResult(FailResultT1(_));

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static T2 FailT2() { Fail(); return default; }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static T2 FailT2(Error _) => FailT2();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<T2> FailTaskT2() => Task.FromResult(FailT2());
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<T2> FailTaskT2(Error _) => FailTaskT2();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Result<T2> FailResultT2() => Result.Success(FailT2());
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Result<T2> FailResultT2(Error _) => FailResultT2();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<Result<T2>> FailResultTaskT2() => Task.FromResult(FailResultT2());
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<Result<T2>> FailResultTaskT2(Error _) => FailResultTaskT2();

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static T2 FailT1T2(T1 _) => FailT2();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<T2> FailTaskT1T2(T1 _) => FailTaskT2();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Result<T2> FailResultT1T2(T1 _) => FailResultT2();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected static Task<Result<T2>> FailResultTaskT1T2(T1 _) => FailResultTaskT2();



    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void BaseAssertSuccess(Result output)
    {
        FuncExecuted.ShouldBeTrue();
        output.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void BaseAssertSuccess<T>(T expected, Result<T> output)
    {
        BaseAssertSuccess(output);
        output.Value.ShouldBe(expected);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void BaseAssertFailure(Result output)
    {
        FuncExecuted.ShouldBeFalse();
        output.IsFailure.ShouldBeTrue();
        output.Error.ShouldBe(NullError);
    }
}
