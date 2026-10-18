using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Documents the <c>errorCodes</c> member that <see cref="CustomResults"/> adds to every validation problem: the
/// generated <see cref="HttpValidationProblemDetails"/> schema gets it with the same shape as its <c>errors</c> map,
/// because both map a field to a list of strings.
/// </summary>
internal sealed class ValidationProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string ErrorsMember = "errors";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(HttpValidationProblemDetails)
            && schema.Properties is { } properties
            && properties.TryGetValue(ErrorsMember, out var errors))
        {
            properties[CustomResults.ErrorCodesMember] = errors;
        }

        return Task.CompletedTask;
    }
}
