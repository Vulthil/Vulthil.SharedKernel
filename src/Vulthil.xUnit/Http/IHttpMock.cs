using System.Net;

namespace Vulthil.xUnit.Http;

/// <summary>
/// An in-process fake for an outbound <see cref="HttpClient"/> dependency. Configure stubbed responses per test
/// and inspect the requests the system under test sent. Register one per typed client via
/// <c>AddHttpMock&lt;TClient, TImplementation&gt;()</c> on the factory and retrieve it with <c>HttpMock&lt;TClient&gt;()</c>.
/// </summary>
public interface IHttpMock : IResettableResource
{
    /// <summary>
    /// Gets the requests received by this mock since the last reset, in the order they were sent.
    /// </summary>
    IReadOnlyList<CapturedHttpRequest> ReceivedRequests { get; }

    /// <summary>
    /// Begins configuring a stubbed response for requests whose method and absolute path match.
    /// The <paramref name="pathPattern"/> is matched against the request's absolute path and supports <c>*</c> wildcards.
    /// </summary>
    /// <param name="method">The HTTP method to match.</param>
    /// <param name="pathPattern">The absolute-path pattern to match, e.g. <c>/users/*/repos</c>.</param>
    /// <returns>A builder for configuring the response.</returns>
    IHttpMockResponseBuilder On(HttpMethod method, string pathPattern);

    /// <summary>
    /// Begins configuring a stubbed response for requests matching a custom predicate.
    /// </summary>
    /// <param name="predicate">The request predicate to match.</param>
    /// <returns>A builder for configuring the response.</returns>
    IHttpMockResponseBuilder On(Func<HttpRequestMessage, bool> predicate);
}

/// <summary>
/// Configures the response a matched request should receive. Returned by the <c>On</c> methods on <see cref="IHttpMock"/>.
/// Header methods apply to the response defined by the preceding <c>RespondWith</c> call and can be chained.
/// </summary>
public interface IHttpMockResponseBuilder
{
    /// <summary>
    /// Responds with the given status code and a JSON body produced by serializing <paramref name="body"/>
    /// using web (camelCase) defaults.
    /// </summary>
    /// <typeparam name="TResponse">The response body type.</typeparam>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="body">The object to serialize as the JSON response body.</param>
    /// <returns>The same builder for chaining.</returns>
    IHttpMockResponseBuilder RespondWith<TResponse>(HttpStatusCode statusCode, TResponse body);

    /// <summary>
    /// Responds with the given status code and no body.
    /// </summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <returns>The same builder for chaining.</returns>
    IHttpMockResponseBuilder RespondWith(HttpStatusCode statusCode);

    /// <summary>
    /// Responds using a custom responder for full control over the <see cref="HttpResponseMessage"/>. The responder
    /// must return a fresh message on each invocation.
    /// </summary>
    /// <param name="responder">The function that produces the response.</param>
    /// <returns>The same builder for chaining.</returns>
    IHttpMockResponseBuilder RespondWith(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder);

    /// <summary>
    /// Responds with the given status code and the supplied JSON string verbatim. Useful for replaying a real
    /// response captured from the live endpoint and stored as a JSON document.
    /// </summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <param name="json">The raw JSON to return as the response body.</param>
    /// <returns>The same builder for chaining.</returns>
    IHttpMockResponseBuilder RespondWithJson(HttpStatusCode statusCode, string json);

    /// <summary>
    /// Adds a header to the response defined by the preceding <c>RespondWith</c> call. Content headers (such as
    /// <c>Content-Type</c>) are routed to the response content automatically.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="values">One or more header values.</param>
    /// <returns>The same builder for chaining.</returns>
    IHttpMockResponseBuilder WithHeader(string name, params string[] values);
}

/// <summary>
/// A snapshot of a request received by an <see cref="IHttpMock"/>, captured for assertions.
/// </summary>
/// <param name="method">The HTTP method.</param>
/// <param name="requestUri">The request URI, or <see langword="null"/> if none was set.</param>
/// <param name="body">The request body as a string, or <see langword="null"/> if there was no body.</param>
public sealed class CapturedHttpRequest(HttpMethod method, Uri? requestUri, string? body)
{
    /// <summary>
    /// Gets the HTTP method of the captured request.
    /// </summary>
    public HttpMethod Method { get; } = method;
    /// <summary>
    /// Gets the URI of the captured request, or <see langword="null"/> if none was set.
    /// </summary>
    public Uri? RequestUri { get; } = requestUri;
    /// <summary>
    /// Gets the body of the captured request as a string, or <see langword="null"/> if there was no body.
    /// </summary>
    public string? Body { get; } = body;
}
