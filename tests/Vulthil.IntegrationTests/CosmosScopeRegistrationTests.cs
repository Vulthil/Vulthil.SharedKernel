using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests;

/// <summary>
/// A host that registers its context itself, pooled, as Aspire's <c>AddCosmosDbContext</c> behind <c>UseCosmos</c>
/// does. The emulator scope runs after the host's registrations, keeps that registration and points it at the
/// scope's database, whose name the scope's connection string carries, as an Aspire database resource's does.
/// </summary>
public sealed class CosmosScopeRegistrationTests(IntegrationTestContainerHost containerHost) : BaseUnitTestCase
{
    [Fact]
    public async Task AScopeKeepsAPooledContextAndPointsItAtTheScopeDatabase()
    {
        // Arrange
        var container = containerHost.Containers.OfType<CosmosTestContainer>().Single();
        await containerHost.EnsureStartedAsync(container);
        var scope = (ITestContainerWithConnectionString)container.CreateScope($"pooled-{Guid.NewGuid():N}");
        await using var scopeLifetime = scope;
        await scope.InitializeAsync();

        var services = new ServiceCollection();
        services.AddDbContextPool<CosmosProbeDbContext>(options =>
            options.UseCosmos("AccountEndpoint=https://unused.invalid:8081/;AccountKey=dGVzdA==", "unused"));

        // Act
        scope.ConfigureServices(services);

        // Assert
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var serviceScope = provider.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<CosmosProbeDbContext>();
        scope.ConnectionString.ShouldEndWith($";Database={context.Database.GetCosmosDatabaseId()}");

        await ((IStartupResource)scope).InitializeAsync(serviceScope.ServiceProvider, CancellationToken);
        context.Probes.Add(new CosmosProbe { Id = Guid.NewGuid().ToString(), Value = "pooled" });
        await context.SaveChangesAsync(CancellationToken);
        (await context.Probes.CountAsync(CancellationToken)).ShouldBe(1);
    }
}
