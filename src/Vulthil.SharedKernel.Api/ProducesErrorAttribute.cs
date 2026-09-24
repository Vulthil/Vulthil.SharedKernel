using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Declares that an endpoint or controller action can fail with the given <see cref="ErrorType"/>, so OpenAPI documents
/// the matching problem response. The status code and body schema come from the same mapping the result extensions
/// use at runtime (<see cref="CustomResults"/>), so endpoint code names the errors it can produce and never a status
/// code. Apply one attribute per error type; a <c>500</c> response is documented on every operation without a
/// declaration, because an unclassified failure is always possible.
/// </summary>
/// <remarks>
/// This is a <see cref="ProducesResponseTypeAttribute"/>, so both the minimal API metadata pipeline and the MVC API
/// explorer pick it up without further wiring: apply it to a controller action, a controller class, or a minimal API
/// handler lambda, or use <see cref="ProducesErrorsExtensions.ProducesErrors{TBuilder}"/> on a route builder.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ProducesErrorAttribute : ProducesResponseTypeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProducesErrorAttribute"/> class for the given error classification.
    /// </summary>
    /// <param name="errorType">The error classification the endpoint can fail with.</param>
    public ProducesErrorAttribute(ErrorType errorType)
        : base(ProblemDetailsTypeFor(errorType), CustomResults.GetStatusCode(errorType), "application/problem+json")
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Gets the declared error classification.
    /// </summary>
    public ErrorType ErrorType { get; }

    private static Type ProblemDetailsTypeFor(ErrorType errorType) =>
        errorType == ErrorType.Validation ? typeof(HttpValidationProblemDetails) : typeof(ProblemDetails);
}
