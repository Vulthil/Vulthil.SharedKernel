
namespace Vulthil.SharedKernel.xUnit.Containers;

public interface IDatabaseContainerPool
{
    Task ApplyMigrations(IServiceProvider services, IDatabaseContainer container);
    Task<IDatabaseContainer> GetContainerAsync();
    void ReleaseContainer(IDatabaseContainer container);
}
