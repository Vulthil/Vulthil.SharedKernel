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
    public async Task NonSuccessStatusCodeThrows()
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
        exception.ShouldNotBeNull();
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
