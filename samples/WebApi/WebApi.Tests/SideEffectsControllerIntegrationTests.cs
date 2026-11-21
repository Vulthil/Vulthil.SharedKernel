using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects;
using WebApi.Domain.SideEffects;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class SideEffectsControllerIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task Test_GetInProgress_Endpoint()
    {
        // Arrange
        var command = new CreateMainEntityCommand(Guid.NewGuid().ToString());
        var createResult = await Sender.SendAsync(command, CancellationToken);
        createResult.IsSuccess.ShouldBeTrue();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var response = await Client.GetAsync("api/SideEffects/in-progress", CancellationToken);
            var sideEffects = await response.GetResponseAsync<List<SideEffectDto>>(CancellationToken);

            if (!sideEffects.Exists(s => s.MainEntityId == createResult.Value))
            {
                return Result.Failure<List<SideEffectDto>>(Error.NotFound("SideEffect.NotFound", "No side effects found"));
            }

            return Result.Success(sideEffects);
        }, cancellationToken: CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s => s.MainEntityId == createResult.Value && s.Status is Status.InProgressStatus);
    }

    [Fact]
    public async Task Test_GetById_Endpoint_returns_a_not_found_problem_for_an_unknown_side_effect()
    {
        // Act
        using var response = await Client.GetAsync($"api/SideEffects/{Guid.NewGuid()}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}
