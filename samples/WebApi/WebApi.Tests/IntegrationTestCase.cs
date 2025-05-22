using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit;
using WebApi.Data;
using WebApi.Models;

namespace WebApi.Tests;

public abstract class BaseIntegrationTestCase(CustomWebApplicationFactory factory, ITestOutputHelper? testOutputHelper = null) : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<CustomWebApplicationFactory>
{
    private static IEnumerable<int> GetData(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    public static TheoryData<int> TestData() => [.. GetData(10)];

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task Test_Something(int param)
    {
        TestOutputHelper?.WriteLine("Test run {0}", param);
        // Arrange
        var dbContext = ScopedServices.GetRequiredService<CosmosDbContext>();
        await dbContext.Database.EnsureCreatedAsync(CancellationToken);

        // Act
        var webApiEntity = WebApiEntity.Create(Guid.NewGuid().ToString());
        dbContext.WebApiEntities.Add(webApiEntity);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Assert
        Assert.Single(await dbContext.WebApiEntities.Where(w => w.Id == webApiEntity.Id).ToListAsync(cancellationToken: CancellationToken));
        Assert.Single(await dbContext.OutboxMessages.Where(w => w.Type == typeof(WebApiEntity).AssemblyQualifiedName && w.Content.Contains(webApiEntity.Id.Value.ToString())).ToListAsync(cancellationToken: CancellationToken));
    }

}

public sealed class IntegrationTestCase1(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase(factory, testOutputHelper);

public sealed class IntegrationTestCase2(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

public sealed class IntegrationTestCase3(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

