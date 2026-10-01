using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Runs a minimal-API <see cref="ProblemHttpResult"/> as an MVC action result, so a controller writes an error's
/// problem response through the same result — and the same problem-details service — as a minimal API endpoint.
/// </summary>
/// <param name="problem">The problem result to run.</param>
internal sealed class ProblemHttpActionResult(ProblemHttpResult problem) : IActionResult
{
    /// <summary>
    /// Gets the problem result this action result runs.
    /// </summary>
    public ProblemHttpResult Problem { get; } = problem;

    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Problem.ExecuteAsync(context.HttpContext);
    }
}
