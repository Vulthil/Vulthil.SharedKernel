using System.Net;
using System.Net.Http.Json;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Testing.Tests;

public sealed class HttpResponseMessageExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public async Task DeserializesTheResponseBodyOnSuccess()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TestPayload("Ada")),
        };

        // Act
        var payload = await response.GetResponseAsync<TestPayload>(CancellationToken);

        // Assert
        payload.Name.ShouldBe("Ada");
    }

    [Fact]
    public async Task NullResponseThrowsArgumentNullException()
    {
        // Arrange
        HttpResponseMessage? response = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => response.GetResponseAsync<TestPayload>(CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public async Task NonSuccessStatusCodeThrowsWithTheStatusAndTheBodyInTheMessage()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent.Create(new TestPayload("Ada")),
        };

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => response.GetResponseAsync<TestPayload>(CancellationToken));

        // Assert
        exception.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        exception.Message.ShouldBe("""Response status code does not indicate success: 404 (Not Found). Response body: {"name":"Ada"}""");
    }

    [Fact]
    public async Task NonSuccessStatusCodeWithoutABodyThrowsWithTheStatusOnly()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => response.GetResponseAsync<TestPayload>(CancellationToken));

        // Assert
        exception.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        exception.Message.ShouldBe("Response status code does not indicate success: 502 (Bad Gateway).");
    }

    [Fact]
    public async Task NonSuccessBodyIsCutToItsFirst4096CharactersInTheMessage()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(new string('x', 5000)),
        };

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => response.GetResponseAsync<TestPayload>(CancellationToken));

        // Assert
        exception.Message.ShouldBe(
            "Response status code does not indicate success: 500 (Internal Server Error). Response body: "
            + new string('x', 4096)
            + "... (truncated from 5000 characters)");
    }

    [Fact]
    public async Task NullJsonBodyThrowsInvalidOperationException()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<TestPayload?>(null),
        };

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => response.GetResponseAsync<TestPayload>(CancellationToken));

        // Assert
        exception.Message.ShouldBe("Response content is empty or could not be deserialized.");
    }

    public sealed record TestPayload(string Name);
}
