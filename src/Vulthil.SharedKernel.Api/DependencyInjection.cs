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

#if NET9_0_OR_GREATER
    /// <summary>
    /// Registers OpenAPI services with the default document name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services)
    {
        return services.AddOpenApiServices(DefaultDocumentName, static (OpenApiOptions options) => { });
    }

    /// <summary>
    /// Registers OpenAPI services with the specified document name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="documentName">The OpenAPI document name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services, string documentName)
    {
        return services.AddOpenApiServices(documentName, static (OpenApiOptions options) => { });
    }

    /// <summary>
    /// Registers OpenAPI services with the specified document name and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="documentName">The OpenAPI document name.</param>
    /// <param name="configure">An action to configure OpenAPI options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services, string documentName, Action<OpenApiOptions> configure)
    {
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
#endif
}
