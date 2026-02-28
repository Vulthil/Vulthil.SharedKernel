using Vulthil.Results;

namespace Vulthil.Results.Tests.Results;

/// <summary>
/// Represents the MapResultBaseTestCase.
/// </summary>
public abstract class MapResultBaseTestCase : ResultBaseTestCase
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected T1 FuncT1()
    {
        FuncExecuted = true;
        return T1.Value;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected T2 FuncT2()
    {
        FuncExecuted = true;
        return T2.Value;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<T2> TaskFuncT2()
    {
        FuncExecuted = true;
        return Task.FromResult(T2.Value);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected T2 FuncT1T2(T1 _)
    {
        FuncExecuted = true;
        return T2.Value;
    }
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected Task<T2> TaskFuncT1T2(T1 _)
    {
        FuncExecuted = true;
        return Task.FromResult(T2.Value);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccess(Result output) => BaseAssertSuccess(output);

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertSuccess(Result<T2> output) => BaseAssertSuccess(T2.Value, output);

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected void AssertFailure(Result output) => BaseAssertFailure(output);
}
