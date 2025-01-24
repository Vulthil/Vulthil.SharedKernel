using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.xUnit;
using WebApi.Data;
using WebApi.Models;
using WebApi.Tests;

[assembly: AssemblyFixture(typeof(PostgreSqlPool))]

namespace WebApi.Tests;

public abstract class BaseIntegrationTestCase : BaseIntegrationTestCase<Program>, IClassFixture<CustomWebApplicationFactory>
{
    protected BaseIntegrationTestCase(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Test_Something()
    {
        // Arrange
        var dbContext = ScopedServices.GetRequiredService<WebApiDbContext>();

        // Act
        var webApiEntity = WebApiEntity.Create(Guid.NewGuid().ToString());
        dbContext.WebApiEntities.Add(webApiEntity);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(await dbContext.WebApiEntities.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }
}

public sealed class IntegrationTestCase1 : BaseIntegrationTestCase
{
    public IntegrationTestCase1(CustomWebApplicationFactory factory) : base(factory)
    {
    }
}


public sealed class IntegrationTestCase3 : BaseIntegrationTestCase
{
    public IntegrationTestCase3(CustomWebApplicationFactory factory) : base(factory)
    {
    }
}

public sealed class IntegrationTestCase4 : BaseIntegrationTestCase
{
    public IntegrationTestCase4(CustomWebApplicationFactory factory) : base(factory)
    {
    }
}

