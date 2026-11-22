using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application.Messaging;
using Orders.Application.Fulfillments;
using Orders.Application.Fulfillments.GetById;
using Orders.Application.Fulfillments.GetInProgress;

namespace Orders.Fulfillments;

/// <summary>
/// Demonstrates the MVC controller path kept alongside minimal API endpoints: <see cref="BaseController"/> supplies
/// route conventions and a scoped logger, and <see cref="ResultHttpExtensions"/> translates query results either into
/// an <see cref="IActionResult"/> (<c>ToActionResult</c>) or into typed results that document themselves
/// (<c>ToIResult</c>).
/// </summary>
/// <param name="sender">Dispatches the query to its registered handler.</param>
public sealed class FulfillmentsController(ISender sender) : BaseController
{
    /// <summary>
    /// Gets every fulfillment that is still in progress.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>200 OK with the in-progress fulfillments.</returns>
    [HttpGet("in-progress")]
    public async Task<IActionResult> GetInProgress(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetInProgressFulfillmentsQuery(), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets one fulfillment.
    /// </summary>
    /// <param name="id">The id of the fulfillment.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>200 OK with the fulfillment, or a 404 problem response.</returns>
    [HttpGet("{id:guid}")]
    [ProducesError(ErrorType.NotFound)]
    public async Task<Results<Ok<FulfillmentDto>, ProblemHttpResult>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Getting fulfillment {FulfillmentId}", id);
        var result = await sender.SendAsync(new GetFulfillmentQuery(id), cancellationToken);
        return result.ToIResult();
    }
}
