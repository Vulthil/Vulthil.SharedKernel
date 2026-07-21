using System.Net;
using System.Net.Http.Json;
using Vulthil.xUnit.Http;

namespace Vulthil.xUnit.Tests.Http;

public sealed class HttpMockTests : BaseUnitTestCase
{
    // HttpMock is internal, so it cannot be the generic Target of BaseUnitTestCase<T> (that would leak an internal
    // type into this public class's base-list). This mirrors the same lazy-creation pattern privately instead.
#pragma warning disable IDE0032 // Lazily created via CreateInstance<T>, not a plain auto property.
    private HttpMock? _target;
#pragma warning restore IDE0032

    private HttpMock Target => _target ??= CreateInstance<HttpMock>();

    [Fact]
    public async Task ExactPathMatchReturnsTheConfiguredResponse()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/users/1").RespondWith(HttpStatusCode.OK, new TestPayload("Ada"));
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/users/1"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TestPayload>(CancellationToken);
        payload.ShouldNotBeNull();
        payload.Name.ShouldBe("Ada");
    }

    [Fact]
    public async Task JsonResponseBodyUsesCamelCasePropertyNaming()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/users/1").RespondWith(HttpStatusCode.OK, new TestPayload("Ada"));
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/users/1"), CancellationToken);

        // Assert
        var json = await response.Content.ReadAsStringAsync(CancellationToken);
        json.ShouldContain("\"name\"");
    }

    [Fact]
    public async Task WildcardSegmentMatchesAnyValueButAnchorsTheRemainderOfThePath()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/users/*/repos").RespondWith(HttpStatusCode.OK);
        using var client = CreateClient();

        // Act
        using var matched = await client.GetAsync(Url("/users/42/repos"), CancellationToken);
        using var unmatched = await client.GetAsync(Url("/users/42/repos/extra"), CancellationToken);

        // Assert
        matched.StatusCode.ShouldBe(HttpStatusCode.OK);
        unmatched.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task DifferentMethodOnAMatchingPathDoesNotMatch()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/ping").RespondWith(HttpStatusCode.OK);
        using var client = CreateClient();

        // Act
        using var response = await client.PostAsync(Url("/ping"), content: null, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task PredicateOverloadMatchesArbitraryCriteria()
    {
        // Arrange
        Target.On(request => request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/custom")
            .RespondWith(HttpStatusCode.Accepted);
        using var client = CreateClient();

        // Act
        using var matched = await client.PostAsync(Url("/custom"), content: null, CancellationToken);
        using var unmatched = await client.GetAsync(Url("/custom"), CancellationToken);

        // Assert
        matched.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        unmatched.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task UnmatchedRequestReturnsNotImplementedWithADiagnosticBody()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/nowhere"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
        response.ReasonPhrase.ShouldBe("No matching HTTP mock");
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        body.ShouldContain("GET");
        body.ShouldContain("/nowhere");
    }

    [Fact]
    public async Task FirstMatchingRuleWinsWhenMultipleRulesOverlap()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/x").RespondWith(HttpStatusCode.OK);
        Target.On(HttpMethod.Get, "/x").RespondWith(HttpStatusCode.Accepted);
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/x"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReceivedRequestsCapturesEveryRequestInOrderIncludingUnmatchedOnes()
    {
        // Arrange
        Target.On(HttpMethod.Post, "/echo").RespondWith(HttpStatusCode.OK);
        using var client = CreateClient();
        using var echoContent = new StringContent("hello");

        // Act
        using var first = await client.GetAsync(Url("/unmatched"), CancellationToken);
        using var second = await client.PostAsync(Url("/echo"), echoContent, CancellationToken);

        // Assert
        Target.ReceivedRequests.Count.ShouldBe(2);
        Target.ReceivedRequests[0].Method.ShouldBe(HttpMethod.Get);
        Target.ReceivedRequests[0].RequestUri.ShouldNotBeNull();
        Target.ReceivedRequests[0].RequestUri!.AbsolutePath.ShouldBe("/unmatched");
        Target.ReceivedRequests[1].Method.ShouldBe(HttpMethod.Post);
        Target.ReceivedRequests[1].Body.ShouldBe("hello");
    }

    [Fact]
    public async Task ResetAsyncClearsBothConfiguredRulesAndReceivedRequests()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/ping").RespondWith(HttpStatusCode.OK);
        using var client = CreateClient();
        using var beforeReset = await client.GetAsync(Url("/ping"), CancellationToken);

        // Act
        await Target.ResetAsync(serviceProvider: null!);

        // Assert
        Target.ReceivedRequests.ShouldBeEmpty();
        using var afterReset = await client.GetAsync(Url("/ping"), CancellationToken);
        beforeReset.StatusCode.ShouldBe(HttpStatusCode.OK);
        afterReset.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task ACustomHeaderIsAddedToTheResponseHeaders()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/ping").RespondWith(HttpStatusCode.OK).WithHeader("X-Custom", "value1", "value2");
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/ping"), CancellationToken);

        // Assert
        response.Headers.GetValues("X-Custom").ShouldBe(["value1", "value2"]);
    }

    [Fact]
    public async Task AContentHeaderFallsBackToTheContentHeadersWhenResponseHeadersRejectIt()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/download")
            .RespondWith(HttpStatusCode.OK, new TestPayload("Ada"))
            .WithHeader("Content-Disposition", "attachment; filename=test.json");
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/download"), CancellationToken);

        // Assert
        response.Headers.Any(header => header.Key == "Content-Disposition").ShouldBeFalse();
        response.Content.Headers.GetValues("Content-Disposition").ShouldBe(["attachment; filename=test.json"]);
    }

    [Fact]
    public async Task RespondWithJsonReturnsTheSuppliedBodyVerbatim()
    {
        // Arrange
        const string rawJson = """{"literal":true}""";
        Target.On(HttpMethod.Get, "/raw").RespondWithJson(HttpStatusCode.OK, rawJson);
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/raw"), CancellationToken);

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        body.ShouldBe(rawJson);
    }

    [Fact]
    public async Task RespondWithACustomResponderGrantsFullControlOverTheResponse()
    {
        // Arrange
        Target.On(HttpMethod.Get, "/full-control")
            .RespondWith((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(Url("/full-control"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public void CapturedHttpRequestExposesTheValuesItWasConstructedWith()
    {
        // Arrange
        var uri = Url("/x");

        // Act
        var captured = new CapturedHttpRequest(HttpMethod.Put, uri, "body");

        // Assert
        captured.Method.ShouldBe(HttpMethod.Put);
        captured.RequestUri.ShouldBe(uri);
        captured.Body.ShouldBe("body");
    }

    private static Uri Url(string path) => new($"http://localhost{path}");

    private HttpClient CreateClient() => new(Target.CreateHandler());

    public sealed record TestPayload(string Name);
}
