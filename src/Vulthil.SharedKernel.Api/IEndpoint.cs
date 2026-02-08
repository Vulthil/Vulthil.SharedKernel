using Microsoft.AspNetCore.Routing;

namespace Vulthil.SharedKernel.Api;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
