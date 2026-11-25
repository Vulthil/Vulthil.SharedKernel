using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Provides extension methods for discovering and mapping <see cref="IEndpoint"/> implementations.
/// </summary>
public static class EndpointExtensions
{

    /// <summary>
    /// Registers all concrete, closed <see cref="IEndpoint"/> implementations from the specified assembly.
    /// </summary>
    /// <remarks>
    /// Open generic <see cref="IEndpoint"/> implementations are skipped, since they cannot be resolved
    /// without type arguments and would otherwise fail when <see cref="MapEndpoints{TBuilder}"/> enumerates them.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan for endpoint implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        ServiceDescriptor[] serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    /// <summary>
    /// Maps all registered <see cref="IEndpoint"/> implementations to <paramref name="builder"/>: the web application,
    /// or a route group to map the endpoints under it, for example <c>app.MapGroup("api").MapEndpoints()</c>.
    /// </summary>
    /// <remarks>
    /// Endpoints are resolved from a dedicated service scope rather than the root provider, so that any
    /// scoped or transient dependencies an <see cref="IEndpoint"/> implementation requires are resolved
    /// and disposed correctly instead of being rooted for the lifetime of the application.
    /// </remarks>
    /// <typeparam name="TBuilder">The type of the endpoint route builder.</typeparam>
    /// <param name="builder">The endpoint route builder to map the endpoints to.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder MapEndpoints<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointRouteBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        using IServiceScope scope = builder.ServiceProvider.CreateScope();
        IEnumerable<IEndpoint> endpoints = scope.ServiceProvider.GetRequiredService<IEnumerable<IEndpoint>>();

        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return builder;
    }

}
