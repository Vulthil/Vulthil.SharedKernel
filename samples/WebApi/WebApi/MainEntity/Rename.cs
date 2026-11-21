using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.MainEntities.Update;

namespace WebApi.MainEntity;

public static class Rename
{
    public sealed record Request(string Name);

    /// <summary>
    /// A command without a value answers 204 No Content on success. A reserved name throws a domain exception, which
    /// the problem-details handler answers with 409 like a failed result.
    /// </summary>
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("main-entities/{id:guid}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new UpdateMainEntityNameCommand(id, request.Name), cancellationToken);
                return result.ToIResult();
            })
            .WithName("RenameMainEntity")
            .ProducesErrors(ErrorType.Validation, ErrorType.NotFound, ErrorType.Conflict);
        }
    }
}
