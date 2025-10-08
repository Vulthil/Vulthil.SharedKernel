using Vulthil.Results;

namespace Vulthil.Results.Tests.Results;

public abstract class TapResultBaseTestCase : ResultBaseTestCase
{

    protected T1? Param { get; private set; }

    protected void Func()
    {
        FuncExecuted = true;
    }
    protected Task TaskFunc()
    {
        Func();
        return Task.CompletedTask;
    }

    protected void FuncT1(T1 _)
    {
        Func();
        Param = _;
    }
    protected Task TaskFuncT1(T1 _)
    {
        FuncT1(_);
        return TaskFunc();
    }

    protected void AssertSuccess(Result output) => BaseAssertSuccess(output);

    protected void AssertFailure(Result output) => BaseAssertFailure(output);
}
