using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit;
using WebApi.Data;
using WebApi.Models;

namespace WebApi.Tests;

public abstract class BaseIntegrationTestCase(CustomWebApplicationFactory factory, ITestOutputHelper? testOutputHelper = null) : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Test_Something()
    {
        // Arrange
        var dbContext = ScopedServices.GetRequiredService<WebApiDbContext>();

        // Act
        var webApiEntity = WebApiEntity.Create(Guid.NewGuid().ToString());
        dbContext.WebApiEntities.Add(webApiEntity);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Assert
        Assert.Single(await dbContext.WebApiEntities.ToListAsync(cancellationToken: CancellationToken));
    }
}

public sealed class IntegrationTestCase1(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase(factory, testOutputHelper);

public sealed class IntegrationTestCase2(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

public sealed class IntegrationTestCase3(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

