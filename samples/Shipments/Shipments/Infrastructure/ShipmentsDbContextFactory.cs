using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shipments.Infrastructure;

/// <summary>
/// Used by <c>dotnet ef migrations</c> only. At run time <c>UseMySql</c> detects the server version from the live
/// connection; the design-time tools have no server, so the version is pinned here.
/// </summary>
public sealed class ShipmentsDbContextFactory : IDesignTimeDbContextFactory<ShipmentsDbContext>
{
    public ShipmentsDbContext CreateDbContext(string[] args)
    {
#pragma warning disable S2068 // Credentials should not be hard-coded
        var options = new DbContextOptionsBuilder<ShipmentsDbContext>()
            .UseMySql("Server=localhost;Database=__design__;User=__design__;Password=__design__", ServerVersion.Parse("8.4.0-mysql"))
            .Options;
#pragma warning restore S2068 // Credentials should not be hard-coded

        return new ShipmentsDbContext(options);
    }
}
