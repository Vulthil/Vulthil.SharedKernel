using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProblemDetailsHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = EnrichProblemDetails);
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
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
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
