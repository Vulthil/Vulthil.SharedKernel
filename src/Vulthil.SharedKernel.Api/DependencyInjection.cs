using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Api;

public static class DependencyInjection
{
    public const string DefaultDocumentName = "v1";

    public static IServiceCollection AddOpenApiServices(this IServiceCollection services, string documentName = DefaultDocumentName, Action<OpenApiOptions>? configure = null)
    {
        configure += (OpenApiOptions options) => { };
        services.AddOpenApi(documentName, configure);

        return services;
    }

    public static IEndpointConventionBuilder MapOpenApiEndpoints(this IEndpointRouteBuilder app)
    {
        return app.MapOpenApi();
    }
}
