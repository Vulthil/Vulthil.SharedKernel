using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using Vulthil.xUnit;
using WebApi.Application.WebApiEntity;
using WebApi.Domain.WebApiEntityModel.Events;
using WebApi.Infrastructure.Data;

namespace WebApi.Tests;

public abstract class BaseIntegrationTestCase(CustomWebApplicationFactory factory, ITestOutputHelper? testOutputHelper = null) : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Test_Create()
    {
        // Arrange
        var sender = ScopedServices.GetRequiredService<ISender>();
        var command = new CreateWebApiEntityCommand(Guid.NewGuid().ToString());

        // Act
        var result = await sender.SendAsync(command, CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Test_Get()
    {
        // Arrange
        var sender = ScopedServices.GetRequiredService<ISender>();
        var command = new CreateWebApiEntityCommand(Guid.NewGuid().ToString());
        var result = await sender.SendAsync(command, CancellationToken);
        Assert.True(result.IsSuccess);

        // Act
        var query = new GetWebApiEntityQuery(result.Value);
        var queryResult = await sender.SendAsync(query, CancellationToken);

        // Assert
        Assert.True(queryResult.IsSuccess);
        Assert.Equal(queryResult.Value.Name, command.Name);
    }

    [Fact]
    public async Task Test_Update()
    {
        // Arrange
        var sender = ScopedServices.GetRequiredService<ISender>();
        var command = new CreateWebApiEntityCommand(Guid.NewGuid().ToString());
        var result = await sender.SendAsync(command, CancellationToken);
        Assert.True(result.IsSuccess);

        var updateCommand = new UpdateWebApiEntityCommand(result.Value, Guid.NewGuid().ToString());

        // Act
        var updateResult = await sender.SendAsync(updateCommand, CancellationToken);

        // Assert
        var json = $$"""
                {"Id": { "Value": "{{result.Value}}" } }
            """;
        Assert.True(updateResult.IsSuccess);
        var outboxMessageResult = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var dbContext = ScopedServices.GetRequiredService<WebApiDbContext>();


            var outboxMessage = await dbContext.OutboxMessages.Where(o =>
                    EF.Functions.JsonContains(o.Content, json) &&
                    o.ProcessedOnUtc.HasValue &&
                    o.Type == typeof(WebApiEntityNameUpdatedEvent).AssemblyQualifiedName)
                .FirstOrDefaultAsync();

            if (outboxMessage is null)
            {
                return Result.Failure<OutboxMessage>(Error.NotFound("OutboxMessage.NotFound", "Could not find processed outbox message"));
            }

            return Result.Success(outboxMessage);
        });

        Assert.True(outboxMessageResult.IsSuccess);
    }
}

public sealed class IntegrationTestCase1(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase(factory, testOutputHelper);

public sealed class IntegrationTestCase2(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

public sealed class IntegrationTestCase3(CustomWebApplicationFactory factory) : BaseIntegrationTestCase(factory);

