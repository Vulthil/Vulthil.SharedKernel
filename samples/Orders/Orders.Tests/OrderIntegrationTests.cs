using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Vulthil.SharedKernel.Outbox;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.MainEntities.GetById;
using WebApi.Application.MainEntities.Update;
using WebApi.Domain.MainEntities.Events;
using WebApi.Infrastructure.Data;
using WebApi.MainEntity;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class MainEntityIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task Test_Create_Endpoint()
    {
        // Arrange
        var client = Client;
        var response = await client.PostAsJsonAsync("api/main-entities", new Create.Request("Test Name"), CancellationToken);

        // Act
        var result = await response.GetResponseAsync<Create.Response>(CancellationToken);

        // Assert
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Test_Create_Endpoint_Rejects_An_Empty_Name_With_A_Validation_Problem_Keyed_By_The_Field()
    {
        // Arrange
        var client = Client;

        // Act
        using var response = await client.PostAsJsonAsync("api/main-entities", new Create.Request(string.Empty), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errors"]!["Name"]!.AsArray().Select(message => (string?)message).ShouldBe(["'Name' must not be empty."]);
        problem["errorCodes"]!["Name"]!.AsArray().Select(code => (string?)code).ShouldBe(["NotEmptyValidator"]);
    }

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
                    o.Type == typeof(MainEntityNameUpdatedEvent).FullName)
                .FirstOrDefaultAsync();

            if (outboxMessage is null)
            {
                return Result.Failure<OutboxMessage>(Error.NotFound("OutboxMessage.NotFound", "Could not find processed outbox message"));
            }

            return Result.Success(outboxMessage);
        }, cancellationToken: CancellationToken);

        outboxMessageResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Rename_Endpoint_returns_no_content()
    {
        // Arrange
        var id = await CreateAsync();

        // Act
        using var response = await Client.PutAsJsonAsync($"api/main-entities/{id}", new Rename.Request("Renamed " + Guid.NewGuid()), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Rename_Endpoint_answers_the_reserved_name_domain_exception_with_a_conflict_problem()
    {
        // Arrange
        var id = await CreateAsync();

        // Act
        using var response = await Client.PutAsJsonAsync($"api/main-entities/{id}", new Rename.Request("admin"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        ((string?)problem["detail"]).ShouldBe("The name 'admin' is reserved.");
    }

    [Fact]
    public async Task Rename_Endpoint_lists_the_domain_error_code_of_a_too_long_name()
    {
        // Arrange
        var id = await CreateAsync();

        // Act
        using var response = await Client.PutAsJsonAsync($"api/main-entities/{id}", new Rename.Request(new string('x', 101)), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errorCodes"]!["Name"]!.AsArray().Select(code => (string?)code).ShouldBe(["MainEntity.NameTooLong"]);
    }

    [Fact]
    public async Task Rename_Endpoint_returns_not_found_for_an_unknown_entity()
    {
        // Act
        using var response = await Client.PutAsJsonAsync($"api/main-entities/{Guid.NewGuid()}", new Rename.Request("Anything"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateMany_Endpoint_creates_every_entity()
    {
        // Act
        using var response = await Client.PostAsJsonAsync("api/main-entities/batch", new CreateMany.Request(["First", "Second", "Third"]), CancellationToken);

        // Assert
        var result = await response.GetResponseAsync<CreateMany.Response>(CancellationToken);
        result.Ids.Count.ShouldBe(3);
        result.Ids.ShouldBeUnique();
    }

    [Fact]
    public async Task CreateMany_Endpoint_creates_none_when_one_name_is_invalid()
    {
        // Arrange
        var marker = Guid.NewGuid().ToString();

        // Act
        using var response = await Client.PostAsJsonAsync("api/main-entities/batch", new CreateMany.Request([marker, " "]), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var dbContext = ScopedServices.GetRequiredService<WebApiDbContext>();
        (await dbContext.MainEntities.AnyAsync(e => e.Name == marker, CancellationToken)).ShouldBeFalse();
    }

    private async Task<Guid> CreateAsync()
    {
        var result = await Sender.SendAsync(new CreateMainEntityCommand(Guid.NewGuid().ToString()), CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
