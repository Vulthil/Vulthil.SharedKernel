using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.xUnit;

public abstract class BaseIntegrationTestCase<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    protected BaseWebApplicationFactory<TEntryPoint> Factory { get; }

    private readonly IServiceScope _scope;

    public IServiceProvider ScopedServices { get; }

    protected BaseIntegrationTestCase(BaseWebApplicationFactory<TEntryPoint> factory)
    {
        Factory = factory;
        _scope = factory.Services.CreateScope();
        ScopedServices = _scope.ServiceProvider;
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Factory.ResetDatabase();
        _scope.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.InitializeRespawners();
        await Initialize();
    }
    public virtual ValueTask Initialize() => ValueTask.CompletedTask;
}
