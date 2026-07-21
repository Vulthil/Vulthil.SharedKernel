using Microsoft.AspNetCore.Mvc;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.GetInProgress;

namespace WebApi.SideEffects;

/// <summary>
/// Demonstrates the MVC controller path kept alongside minimal API endpoints: <see cref="BaseController"/> supplies
/// route conventions and a scoped logger, and <see cref="ResultHttpExtensions"/>' <c>ToActionResult</c> translates
/// the query result into the equivalent <see cref="IActionResult"/>.
/// </summary>
/// <param name="sender">Dispatches the query to its registered handler.</param>
public sealed class SideEffectsController(ISender sender) : BaseController
{
    /// <summary>
    /// Gets every side effect that is currently in progress.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>200 OK with the in-progress side effects.</returns>
    [HttpGet("in-progress")]
    public async Task<IActionResult> GetInProgress(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetInProgressQuery(), cancellationToken);
        return result.ToActionResult(this);
    }
}
