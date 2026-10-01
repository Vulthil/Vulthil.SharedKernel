using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Handles otherwise-uncaught exceptions by writing an RFC 7807 <see cref="ProblemDetails"/> response and logging the
/// failure. An exception that carries an error (<see cref="IHasError"/>, such as a domain exception) is answered with
/// the problem response its error maps to — the same response a failed result with that error produces. Every other
/// exception is answered with a generic 500 whose body never includes the exception message.
/// </summary>
/// <param name="problemDetailsService">The service used to write the problem-details response.</param>
/// <param name="logger">The logger used to record the exception.</param>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problemDetails = exception is IHasError { Error: { } error }
            ? CustomResults.CreateProblemDetails(error)
            : CreateUnexpectedErrorProblemDetails();
        var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        Log(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        }).ConfigureAwait(false);
    }

    private static ProblemDetails CreateUnexpectedErrorProblemDetails() => new()
    {
        Title = "An unexpected error occurred",
        Status = StatusCodes.Status500InternalServerError,
        Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1"
    };

    /// <summary>
    /// Logs a server error at <see cref="LogLevel.Error"/>. A client error comes from the error the exception carries,
    /// so the request was answered as designed and the exception is logged at <see cref="LogLevel.Warning"/>.
    /// </summary>
    private void Log(HttpContext httpContext, Exception exception, int statusCode)
    {
        var sanitizedMethod = SanitizeForLog(httpContext.Request.Method);
        var sanitizedPath = SanitizeForLog(httpContext.Request.Path.Value);

        if (statusCode < StatusCodes.Status500InternalServerError)
        {
            logger.LogWarning(
                exception,
                "Exception while processing {Method} {Path} answered with {StatusCode} from the error it carries",
                sanitizedMethod,
                sanitizedPath,
                statusCode);
            return;
        }

        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            sanitizedMethod,
            sanitizedPath);
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
