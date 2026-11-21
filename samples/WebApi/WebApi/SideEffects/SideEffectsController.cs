using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.GetById;
using WebApi.Application.SideEffects.GetInProgress;

namespace WebApi.SideEffects;

/// <summary>
/// Demonstrates the MVC controller path kept alongside minimal API endpoints: <see cref="BaseController"/> supplies
/// route conventions and a scoped logger, and <see cref="ResultHttpExtensions"/> translates query results either into
/// an <see cref="IActionResult"/> (<c>ToActionResult</c>) or into typed results that document themselves
/// (<c>ToIResult</c>).
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

    /// <summary>
    /// Gets one side effect.
    /// </summary>
    /// <param name="id">The id of the side effect.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>200 OK with the side effect, or a 404 problem response.</returns>
    [HttpGet("{id:guid}")]
    [ProducesError(ErrorType.NotFound)]
    public async Task<Results<Ok<SideEffectDto>, ProblemHttpResult>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Getting side effect {SideEffectId}", id);
        var result = await sender.SendAsync(new GetSideEffectQuery(id), cancellationToken);
        return result.ToIResult();
    }
}
