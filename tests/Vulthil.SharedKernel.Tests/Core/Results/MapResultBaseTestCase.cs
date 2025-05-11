using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Tests.Core.Results;

public abstract class MapResultBaseTestCase : ResultBaseTestCase
{
    protected T1 FuncT1()
    {
        FuncExecuted = true;
        return T1.Value;
    }
    protected T2 FuncT2()
    {
        FuncExecuted = true;
        return T2.Value;
    }
    protected Task<T2> TaskFuncT2()
    {
        FuncExecuted = true;
        return Task.FromResult(T2.Value);
    }

    protected T2 FuncT1T2(T1 _)
    {
        FuncExecuted = true;
        return T2.Value;
    }
    protected Task<T2> TaskFuncT1T2(T1 _)
    {
        FuncExecuted = true;
        return Task.FromResult(T2.Value);
    }

    protected void AssertSuccess(Result output) => BaseAssertSuccess(output);

    protected void AssertSuccess(Result<T2> output) => BaseAssertSuccess(T2.Value, output);

    protected void AssertFailure(Result output) => BaseAssertFailure(output);
}
