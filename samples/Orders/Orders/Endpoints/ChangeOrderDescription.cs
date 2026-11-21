using Orders.Application.Orders.ChangeDescription;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;

namespace Orders.Endpoints;

public static class ChangeOrderDescription
{
    public sealed record Request(string Description);

    /// <summary>
    /// A command without a value answers 204 No Content on success, and each failure with its problem response.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("orders/{id:guid}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.SendAsync(new ChangeOrderDescriptionCommand(id, request.Description), cancellationToken);
                    return result.ToIResult();
                })
                .WithName("ChangeOrderDescription")
                .ProducesErrors(ErrorType.Validation, ErrorType.NotFound, ErrorType.Conflict);
    }
}
