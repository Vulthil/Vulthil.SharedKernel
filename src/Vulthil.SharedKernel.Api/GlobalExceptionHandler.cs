using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Handles otherwise-uncaught exceptions by writing an RFC 7807 <see cref="ProblemDetails"/> response
/// and logging the failure. The exception message is never leaked to the response body.
/// </summary>
/// <param name="problemDetailsService">The service used to write the problem-details response.</param>
/// <param name="logger">The logger used to record the unhandled exception.</param>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var sanitizedMethod = SanitizeForLog(httpContext.Request.Method);
        var sanitizedPath = SanitizeForLog(httpContext.Request.Path.Value);

        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            sanitizedMethod,
            sanitizedPath);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1"
            }
        }).ConfigureAwait(false);
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\u2028", string.Empty, StringComparison.Ordinal)
            .Replace("\u2029", string.Empty, StringComparison.Ordinal);
    }
}
