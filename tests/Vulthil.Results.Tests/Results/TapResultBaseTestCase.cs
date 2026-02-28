using Vulthil.Results;

namespace Vulthil.Results.Tests.Results;

/// <summary>
/// Represents the TapResultBaseTestCase.
/// </summary>
public abstract class TapResultBaseTestCase : ResultBaseTestCase
{

    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    protected T1? Param { get; private set; }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void Func()
    {
        FuncExecuted = true;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task TaskFunc()
    {
        Func();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void FuncT1(T1 _)
    {
        Func();
        Param = _;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task TaskFuncT1(T1 _)
    {
        FuncT1(_);
        return TaskFunc();
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
