using Microsoft.AspNetCore.OpenApi;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// An OpenAPI operation transformer that documents error responses as RFC 7807 ProblemDetails,
/// ensuring every operation advertises a <c>500</c> response served as <c>application/problem+json</c>
/// with a ProblemDetails schema attached.
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
        var schema = CreateProblemDetailsSchema();

        if (responses.TryGetValue(statusCode, out var existingResponse))
        {
            if (existingResponse.Content is { } existingContent)
            {
                AddProblemJsonContent(existingContent, schema);
                return;
            }

            if (existingResponse is OpenApiResponse concreteResponse)
            {
                concreteResponse.Content = CreateProblemJsonContent(schema);
                return;
            }

            responses[statusCode] = new OpenApiResponse
            {
                Description = existingResponse.Description,
                Content = CreateProblemJsonContent(schema),
                Headers = existingResponse.Headers,
                Links = existingResponse.Links,
                Extensions = existingResponse.Extensions,
            };

            return;
        }

        responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content = CreateProblemJsonContent(schema)
        };
    }

    private static Dictionary<string, OpenApiMediaType> CreateProblemJsonContent(OpenApiSchema schema)
    {
        var content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
        AddProblemJsonContent(content, schema);

        return content;
    }

    private static void AddProblemJsonContent(IDictionary<string, OpenApiMediaType> content, OpenApiSchema schema)
    {
        if (content.TryGetValue(ProblemJsonMediaType, out var mediaType))
        {
            mediaType.Schema ??= schema;

            return;
        }

        content[ProblemJsonMediaType] = new OpenApiMediaType { Schema = schema };
    }

#if NET10_0_OR_GREATER
    private static OpenApiSchema CreateProblemDetailsSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String },
        },
        AdditionalPropertiesAllowed = true,
    };
#else
    private static OpenApiSchema CreateProblemDetailsSchema() => new()
    {
        Type = "object",
        Properties = new Dictionary<string, OpenApiSchema>(StringComparer.Ordinal)
        {
            ["type"] = new OpenApiSchema { Type = "string" },
            ["title"] = new OpenApiSchema { Type = "string" },
            ["status"] = new OpenApiSchema { Type = "integer", Format = "int32" },
            ["detail"] = new OpenApiSchema { Type = "string" },
            ["instance"] = new OpenApiSchema { Type = "string" },
        },
        AdditionalPropertiesAllowed = true,
    };
#endif
}
