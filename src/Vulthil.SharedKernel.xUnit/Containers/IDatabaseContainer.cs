using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.xUnit.Containers;

public interface IDatabaseContainer : IAsyncDisposable
{
    Func<Action<DbContextOptionsBuilder>> OptionsAction { get; }
    Type DbContextType { get; }
    bool HasBeenMigrated { get; }

    Task InitializeRespawner();
    void MarkMigrated();
    Task ResetAsync();
}
