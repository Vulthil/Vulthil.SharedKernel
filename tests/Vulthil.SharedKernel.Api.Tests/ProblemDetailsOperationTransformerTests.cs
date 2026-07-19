using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit;
#if NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class ProblemDetailsOperationTransformerTests : BaseUnitTestCase
{
    private const string ProblemJsonMediaType = "application/problem+json";

    private readonly Lazy<ProblemDetailsOperationTransformer> _lazyTarget;
    private ProblemDetailsOperationTransformer Target => _lazyTarget.Value;

    public ProblemDetailsOperationTransformerTests()
    {
        _lazyTarget = new(CreateInstance<ProblemDetailsOperationTransformer>);
    }

    [Fact]
    public async Task TransformAsyncAddsMissing500ResponseWithProblemJsonSchema()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await Target.TransformAsync(operation, CreateContext(), CancellationToken);

        // Assert
        var response = operation.Responses!["500"];
        Assert.False(string.IsNullOrEmpty(response.Description));
        var mediaType = response.Content![ProblemJsonMediaType];
        Assert.NotNull(mediaType.Schema);
    }

    [Fact]
    public async Task TransformAsyncMergesExisting500ResponseWithoutContentPreservingDescription()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["500"] = new OpenApiResponse { Description = "Custom description", Content = null }
            }
        };

        // Act
        await Target.TransformAsync(operation, CreateContext(), CancellationToken);

        // Assert
        var response = operation.Responses["500"];
        Assert.Equal("Custom description", response.Description);
        var mediaType = response.Content![ProblemJsonMediaType];
        Assert.NotNull(mediaType.Schema);
    }

    [Fact]
    public async Task TransformAsyncMergesExisting500ResponseWithContentPreservingDescriptionAndExistingMediaTypes()
    {
        // Arrange
        var existingContent = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["application/json"] = new OpenApiMediaType()
        };
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["500"] = new OpenApiResponse { Description = "Custom description", Content = existingContent }
            }
        };

        // Act
        await Target.TransformAsync(operation, CreateContext(), CancellationToken);

        // Assert
        var response = operation.Responses["500"];
        Assert.Equal("Custom description", response.Description);
        Assert.True(response.Content!.ContainsKey("application/json"));
        var mediaType = response.Content[ProblemJsonMediaType];
        Assert.NotNull(mediaType.Schema);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public async Task TransformAsyncAttachesObjectSchemaWithProblemDetailsProperties()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await Target.TransformAsync(operation, CreateContext(), CancellationToken);

        // Assert
        var schema = Assert.IsType<OpenApiSchema>(operation.Responses!["500"].Content![ProblemJsonMediaType].Schema);
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Contains("detail", schema.Properties!.Keys);
        Assert.Contains("status", schema.Properties!.Keys);
    }
#else
    [Fact]
    public async Task TransformAsyncAttachesObjectSchemaWithProblemDetailsProperties()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await Target.TransformAsync(operation, CreateContext(), CancellationToken);

        // Assert
        var schema = operation.Responses!["500"].Content![ProblemJsonMediaType].Schema;
        Assert.Equal("object", schema.Type);
        Assert.Contains("detail", schema.Properties.Keys);
        Assert.Contains("status", schema.Properties.Keys);
    }
#endif

    private static OpenApiOperationTransformerContext CreateContext() => new()
    {
        DocumentName = "v1",
        Description = new ApiDescription(),
        ApplicationServices = new ServiceCollection().BuildServiceProvider(),
    };
}
