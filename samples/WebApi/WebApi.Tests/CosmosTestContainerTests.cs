using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.xUnit;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

[Collection(CosmosCollection.Name)]
public sealed class CosmosTestContainerTests(CosmosWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<CosmosWebApplicationFactory, Program>(factory, testOutputHelper)
{
    [Fact]
    public async Task ContainerStartsWithAnEmptyDatabase()
    {
        // Act
        var context = ScopedServices.GetRequiredService<CosmosProbeDbContext>();

        // Assert
        (await context.Probes.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task WritesAndReadsBackAnEntity()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var write = ScopedServices.GetRequiredService<CosmosProbeDbContext>();
        write.Probes.Add(new CosmosProbe { Id = id, Value = "hello" });
        await write.SaveChangesAsync(CancellationToken);
        await ResetScope();

        // Act
        var read = ScopedServices.GetRequiredService<CosmosProbeDbContext>();
        var probe = await read.Probes.SingleAsync(candidate => candidate.Id == id, CancellationToken);

        // Assert
        probe.Value.ShouldBe("hello");
    }

    [Fact]
    public Task EachTestStartsFromACleanDatabaseFirst() => WriteOneProbeAndAssertSingle();

    [Fact]
    public Task EachTestStartsFromACleanDatabaseSecond() => WriteOneProbeAndAssertSingle();

    private async Task WriteOneProbeAndAssertSingle()
    {
        var context = ScopedServices.GetRequiredService<CosmosProbeDbContext>();
        context.Probes.Add(new CosmosProbe { Id = Guid.NewGuid().ToString(), Value = "single" });
        await context.SaveChangesAsync(CancellationToken);

        (await context.Probes.CountAsync(CancellationToken)).ShouldBe(1);
    }
}
