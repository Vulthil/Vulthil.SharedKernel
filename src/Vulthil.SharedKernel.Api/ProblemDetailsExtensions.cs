using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Provides extension methods for wiring RFC 7807 ProblemDetails responses across the request pipeline,
/// covering handled failures, uncaught exceptions, and bare error status codes.
/// </summary>
public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Registers ProblemDetails generation and the global exception handler.
    /// </summary>
    /// <remarks>
    /// Every produced <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> is enriched with the request
    /// method and path as the instance, the request identifier, and the current trace identifier when available.
    /// This enrichment composes with any consumer-supplied <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/>
    /// delegate (configured via <see cref="Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions.Configure{TOptions}(IServiceCollection, Action{TOptions})"/>
    /// or another call to <c>AddProblemDetails</c>) rather than overwriting it, regardless of registration order.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProblemDetailsHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.PostConfigure<ProblemDetailsOptions>(ComposeProblemDetailsEnrichment);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Adds the exception-handling and status-code-pages middleware that produce ProblemDetails responses.
    /// </summary>
    /// <remarks>
    /// Register this as the first middleware so it wraps the entire request pipeline. The parameterless
    /// exception handler relies on the registered <see cref="IProblemDetailsService"/>.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseProblemDetailsHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    private static void ComposeProblemDetailsEnrichment(ProblemDetailsOptions options)
    {
        var existingCustomization = options.CustomizeProblemDetails;
        options.CustomizeProblemDetails = context =>
        {
            existingCustomization?.Invoke(context);
            EnrichProblemDetails(context);
        };
    }

    private static void EnrichProblemDetails(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        var problemDetails = context.ProblemDetails;

        problemDetails.Instance ??= $"{httpContext.Request.Method} {httpContext.Request.Path}";
        problemDetails.Extensions["requestId"] = httpContext.TraceIdentifier;

        var traceId = Activity.Current?.Id;
        if (traceId is not null)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }
    }
}
