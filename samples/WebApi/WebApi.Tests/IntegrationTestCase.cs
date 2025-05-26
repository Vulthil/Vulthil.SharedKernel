using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
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
        var sender = ScopedServices.GetRequiredService<ISender>();
        var command = new CreateWebApiEntityCommand(Guid.NewGuid().ToString());
        var dbContext = ScopedServices.GetRequiredService<WebApiDbContext>();

        // Act
        var result = await sender.SendAsync(command, CancellationToken);


        // Assert
        Assert.True(result.IsSuccess);
        var query = new GetWebApiEntityQuery(result.Value);

        var queryResult = await sender.SendAsync(query, CancellationToken);

        Assert.True(queryResult.IsSuccess);
        Assert.Equal(queryResult.Value.Name, command.Name);

    }
}

public sealed class IntegrationTestCase1(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase(factory, testOutputHelper);

public sealed class IntegrationTestCase2(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

public sealed class IntegrationTestCase3(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

