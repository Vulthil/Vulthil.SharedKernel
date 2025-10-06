using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Fixtures;


namespace Vulthil.xUnit;

public abstract class BaseWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private TestFixture? _testFixture;
    internal void SetFixture(TestFixture testFixture) => _testFixture = testFixture;
    protected virtual void ConfigureCustomWebHost(IWebHostBuilder builder) { }
    protected override sealed void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var container in _testFixture?.ContainersWithConnectionStrings ?? [])
        {
            var connectionString = container.ConnectionString;
            builder.UseSetting($"ConnectionStrings:{container.ConnectionStringKey}", connectionString);
        }

        ConfigureCustomWebHost(builder);

        builder.ConfigureServices(services =>
        {

            var serviceProvider = services.BuildServiceProvider();

            // Create a scope for the migration
            using var scope = serviceProvider.CreateScope();

            var scopedServices = scope.ServiceProvider;
            _testFixture?.MigrateDatabases(scopedServices).GetAwaiter().GetResult();
            _testFixture?.InitializeRespawners().GetAwaiter().GetResult();
        });
    }
}
