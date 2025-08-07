using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects.GetInProgress;

namespace WebApi.Tests;

public sealed class SideEffectIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task TestCreate()
    {
        // Arrange
        var sender = ScopedServices.GetRequiredService<ISender>();
        var command = new CreateMainEntityCommand(Guid.NewGuid().ToString());
        var createResult = await sender.SendAsync(command, CancellationToken);
        createResult.IsSuccess.ShouldBeTrue();

        var query = new GetInProgressQuery();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var queryResult = await sender.SendAsync(query, CancellationToken);

            if (queryResult.IsFailure)
            {
                return Result.Failure<List<SideEffectDto>>(Error.NullValue);
            }

            if (queryResult.Value.Count(s => s.MainEntityId == createResult.Value) == 0)
            {
                return Result.Failure<List<SideEffectDto>>(Error.NotFound("NotFound", "No side effects found"));
            }

            return queryResult;
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s =>
            s.MainEntityId == createResult.Value && s.Status == StatusEnum.InProgress);
    }
}


