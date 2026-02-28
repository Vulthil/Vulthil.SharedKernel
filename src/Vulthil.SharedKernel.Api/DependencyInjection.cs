using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Provides extension methods for registering API-layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// The default OpenAPI document name.
    /// </summary>
    public const string DefaultDocumentName = "v1";

    /// <summary>
    /// Registers OpenAPI services with the specified document name and optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="documentName">The OpenAPI document name.</param>
    /// <param name="configure">An optional action to configure OpenAPI options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services, string documentName = DefaultDocumentName, Action<OpenApiOptions>? configure = null)
    {
        configure += (OpenApiOptions options) => { };
        services.AddOpenApi(documentName, configure);

        return services;
    }

    /// <summary>
    /// Maps OpenAPI endpoints to the application's route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapOpenApiEndpoints(this IEndpointRouteBuilder app)
    {
        return app.MapOpenApi();
    }
}
