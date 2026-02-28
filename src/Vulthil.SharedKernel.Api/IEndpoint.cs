using Microsoft.AspNetCore.Routing;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Defines a minimal API endpoint that registers its routes during application startup.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Maps the endpoint to the provided route builder.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    void MapEndpoint(IEndpointRouteBuilder app);
}
