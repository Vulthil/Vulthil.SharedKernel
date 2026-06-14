using Microsoft.AspNetCore.OpenApi;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// An OpenAPI operation transformer that documents error responses as RFC 7807 ProblemDetails,
/// ensuring every operation advertises a <c>500</c> response served as <c>application/problem+json</c>.
/// </summary>
internal sealed class ProblemDetailsOperationTransformer : IOpenApiOperationTransformer
{
    private const string ProblemJsonMediaType = "application/problem+json";
    private const string ServerErrorStatusCode = "500";
    private const string ServerErrorDescription = "An unexpected error occurred. The response body is an RFC 7807 problem details document.";

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        operation.Responses ??= new OpenApiResponses();

        EnsureProblemDetailsResponse(operation.Responses, ServerErrorStatusCode, ServerErrorDescription);

        return Task.CompletedTask;
    }

    private static void EnsureProblemDetailsResponse(OpenApiResponses responses, string statusCode, string description)
    {
        if (responses.TryGetValue(statusCode, out var existingResponse) && existingResponse.Content is { } existingContent)
        {
            AddProblemJsonContent(existingContent);

            return;
        }

        var content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
        AddProblemJsonContent(content);

        responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content = content
        };
    }

    private static void AddProblemJsonContent(IDictionary<string, OpenApiMediaType> content)
    {
        if (!content.ContainsKey(ProblemJsonMediaType))
        {
            content[ProblemJsonMediaType] = new OpenApiMediaType();
        }
    }
}
