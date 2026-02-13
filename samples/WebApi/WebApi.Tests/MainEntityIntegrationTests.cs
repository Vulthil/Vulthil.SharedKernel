using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.MainEntities.GetById;
using WebApi.Application.MainEntities.Update;
using WebApi.Domain.MainEntities.Events;
using WebApi.Infrastructure.Data;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class MainEntityIntegrationTests(FixtureWrapper testFixture, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(testFixture, testOutputHelper)
{
    [Fact]
    public async Task Test_Create()
    {
        // Arrange
        var command = new CreateMainEntityCommand(Guid.NewGuid().ToString());

        // Act
        var result = await Sender.SendAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Test_Get()
    {
        // Arrange
        var command = new CreateMainEntityCommand(Guid.NewGuid().ToString());
        var result = await Sender.SendAsync(command, CancellationToken);
        result.IsSuccess.ShouldBeTrue();

        // Act
        var query = new GetMainEntityByIdQuery(result.Value);
        var queryResult = await Sender.SendAsync(query, CancellationToken);

        // Assert
        queryResult.IsSuccess.ShouldBeTrue();
        queryResult.Value.Name.ShouldBe(command.Name);
    }

    [Fact]
    public async Task Test_Update()
    {
        // Arrange
        var command = new CreateMainEntityCommand(Guid.NewGuid().ToString());
        var result = await Sender.SendAsync(command, CancellationToken);
        result.IsSuccess.ShouldBeTrue();

        var updateCommand = new UpdateMainEntityNameCommand(result.Value, Guid.NewGuid().ToString());

        // Act
        var updateResult = await Sender.SendAsync(updateCommand, CancellationToken);

        // Assert
        var json = $$"""
                {"Id": { "Value": "{{result.Value}}" } }
            """;

        updateResult.IsSuccess.ShouldBeTrue();
        var outboxMessageResult = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var dbContext = ScopedServices.GetRequiredService<WebApiDbContext>();

            var outboxMessage = await dbContext.OutboxMessages.Where(o =>
                    EF.Functions.JsonContains(o.Content, json) &&
                    o.ProcessedOnUtc.HasValue &&
                    o.Type == typeof(MainEntityNameUpdatedEvent).AssemblyQualifiedName)
                .FirstOrDefaultAsync();

            if (outboxMessage is null)
            {
                return Result.Failure<OutboxMessage>(Error.NotFound("OutboxMessage.NotFound", "Could not find processed outbox message"));
            }

            return Result.Success(outboxMessage);
        });

        outboxMessageResult.IsSuccess.ShouldBeTrue();
    }
}


