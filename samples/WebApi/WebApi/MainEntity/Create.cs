using Microsoft.AspNetCore.Http.HttpResults;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Create;

namespace WebApi.MainEntity;

/// <summary>
/// Represents the Create.
/// </summary>
public static class Create
{
    /// <summary>
    /// Represents the Request.
    /// </summary>
    public sealed record Request(string Name);

    /// <summary>
    /// The response for the Create endpoint, containing the unique identifier of the newly created MainEntity.
    /// </summary>
    /// <param name="Id">The id of the newly created MainEntity.</param>
    public sealed record Response(Guid Id);

    /// <summary>
    /// Represents the Endpoint.
    /// </summary>
    public class Endpoint : IEndpoint
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("main-entities", async Task<Results<CreatedAtRoute<Response>, ValidationProblem, NotFound, Conflict, ProblemHttpResult>> (ICommandHandler<CreateMainEntityCommand, Result<Guid>> handler, Request request) =>
            {
                var command = new CreateMainEntityCommand(request.Name);
                var result = await handler.HandleAsync(command);
                return result
                    .Map(id => new Response(id))
                    .ToCreatedAtRouteHttpResult("GetMainEntity", r => r);
            });
        }

    }
}
