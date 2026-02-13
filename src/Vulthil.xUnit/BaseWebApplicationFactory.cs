using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        builder.ConfigureServices(services => services.Insert(0, ServiceDescriptor.Singleton<IHostedService>(
                    sp => new TestMigrationHostedService(() => _testFixture, sp))));

        ConfigureCustomWebHost(builder);
    }
}

internal sealed class TestMigrationHostedService(Func<TestFixture?> testFixtureAction, IServiceProvider serviceProvider) : IHostedService
{
    private bool _completed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        var testFixture = testFixtureAction();

        if(testFixture is null)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        await testFixture.MigrateDatabases(scope.ServiceProvider);
        await testFixture.InitializeRespawners();
        _completed = true;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
