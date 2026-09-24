using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

/// <summary>
/// Generates a real OpenAPI document from an in-memory host to pin the documentation contract of the typed-result
/// extensions: an operation documents its success status, a <c>500</c>, and exactly the error types it declares —
/// nothing else — whether the declaration comes from <c>ProducesErrors</c> or from <see cref="ProducesErrorAttribute"/>.
/// </summary>
public sealed class DeclaredErrorsOpenApiDocumentTests : BaseUnitTestCase
{
    [Fact]
    public async Task DeclaredErrorsAreDocumentedAndUndeclaredOnesAreNot()
    {
        // Arrange
        await using var app = await StartHostAsync(host =>
        {
            host.MapGet("/declared", () => Result.Success(1).ToIResult())
                .ProducesErrors(ErrorType.NotFound, ErrorType.Validation);
            host.MapGet("/bare", () => Result.Success(1).ToIResult());
        });

        // Act
        using var document = await ReadDocumentAsync(app);

        // Assert
        ResponseCodes(document, "/declared", "get").ShouldBe(["200", "400", "404", "500"]);
        ResponseCodes(document, "/bare", "get").ShouldBe(["200", "500"]);
    }

    [Fact]
    public async Task AnAttributeOnTheHandlerDocumentsTheDeclaredError()
    {
        // Arrange
        await using var app = await StartHostAsync(host =>
            host.MapDelete("/attributed", [ProducesError(ErrorType.Conflict)] () => Result.Success().ToIResult()));

        // Act
        using var document = await ReadDocumentAsync(app);

        // Assert
        ResponseCodes(document, "/attributed", "delete").ShouldBe(["204", "409", "500"]);
    }

    [Fact]
    public async Task DeclaredErrorsAreDocumentedAsProblemJson()
    {
        // Arrange
        await using var app = await StartHostAsync(host =>
            host.MapGet("/declared", () => Result.Success(1).ToIResult()).ProducesErrors(ErrorType.NotFound));

        // Act
        using var document = await ReadDocumentAsync(app);

        // Assert
        var notFound = document.RootElement
            .GetProperty("paths").GetProperty("/declared").GetProperty("get").GetProperty("responses").GetProperty("404");
        notFound.GetProperty("content").TryGetProperty("application/problem+json", out _).ShouldBeTrue();
    }

    private static async Task<WebApplication> StartHostAsync(Action<WebApplication> mapEndpoints)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddOpenApiServices();

        var app = builder.Build();
        mapEndpoints(app);
        app.MapOpenApiEndpoints();
        await app.StartAsync(CancellationToken);

        return app;
    }

    private static async Task<JsonDocument> ReadDocumentAsync(WebApplication app)
    {
        using var client = app.GetTestClient();
        var documentUri = new Uri($"/openapi/{DependencyInjection.DefaultDocumentName}.json", UriKind.Relative);
        var json = await client.GetStringAsync(documentUri, CancellationToken);

        return JsonDocument.Parse(json);
    }

    private static string[] ResponseCodes(JsonDocument document, string path, string verb) =>
        [.. document.RootElement
            .GetProperty("paths").GetProperty(path).GetProperty(verb).GetProperty("responses")
            .EnumerateObject()
            .Select(response => response.Name)
            .Order()];
}
