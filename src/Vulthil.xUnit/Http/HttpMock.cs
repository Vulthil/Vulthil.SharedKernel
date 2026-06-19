using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vulthil.xUnit.Http;

/// <summary>
/// Default in-process <see cref="IHttpMock"/> implementation. Holds the configured response rules and captured
/// requests as durable state, and hands out lightweight <see cref="HttpMessageHandler"/> instances (which the
/// HTTP client factory owns and may dispose) that delegate back to this shared state.
/// </summary>
public sealed class HttpMock : IHttpMock
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private readonly List<MockRule> _rules = [];
    private readonly List<CapturedHttpRequest> _received = [];

    /// <inheritdoc />
    public IReadOnlyList<CapturedHttpRequest> ReceivedRequests
    {
        get
        {
            lock (_gate)
            {
                return _received.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public IHttpMockResponseBuilder On(HttpMethod method, string pathPattern)
    {
        var pathRegex = BuildPathRegex(pathPattern);
        return On(request => request.Method == method
            && pathRegex.IsMatch(request.RequestUri?.AbsolutePath ?? string.Empty));
    }

    /// <inheritdoc />
    public IHttpMockResponseBuilder On(Func<HttpRequestMessage, bool> predicate)
    {
        var rule = new MockRule(predicate);
        lock (_gate)
        {
            _rules.Add(rule);
        }
        return rule;
    }

    /// <inheritdoc />
    public ValueTask ResetAsync(IServiceProvider serviceProvider)
    {
        lock (_gate)
        {
            _rules.Clear();
            _received.Clear();
        }
        return ValueTask.CompletedTask;
    }

    internal HttpMessageHandler CreateHandler() => new HttpMockHandler(this);

    private async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        MockRule? matched;
        lock (_gate)
        {
            _received.Add(new CapturedHttpRequest(request.Method, request.RequestUri, body));
            matched = _rules.FirstOrDefault(rule => rule.Matches(request));
        }

        if (matched is null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotImplemented)
            {
                ReasonPhrase = "No matching HTTP mock",
                Content = new StringContent(
                    $"No HTTP mock response configured for {request.Method} {request.RequestUri}.",
                    Encoding.UTF8,
                    "text/plain"),
            };
        }

        return await matched.RespondAsync(request, cancellationToken);
    }

    private static Regex BuildPathRegex(string pathPattern)
    {
        var escaped = Regex.Escape(pathPattern).Replace("\\*", ".*", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }

    private sealed class MockRule(Func<HttpRequestMessage, bool> matches) : IHttpMockResponseBuilder
    {
        private readonly List<KeyValuePair<string, string>> _headers = [];
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string? _body;
        private string _mediaType = "application/json";
        private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _responder;

        public Func<HttpRequestMessage, bool> Matches { get; } = matches;

        public IHttpMockResponseBuilder RespondWith<TResponse>(HttpStatusCode statusCode, TResponse body)
        {
            _statusCode = statusCode;
            _body = JsonSerializer.Serialize(body, JsonOptions);
            _mediaType = "application/json";
            _responder = null;
            return this;
        }

        public IHttpMockResponseBuilder RespondWith(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
            _body = null;
            _responder = null;
            return this;
        }

        public IHttpMockResponseBuilder RespondWith(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
            return this;
        }

        public IHttpMockResponseBuilder RespondWithJson(HttpStatusCode statusCode, string json)
        {
            _statusCode = statusCode;
            _body = json;
            _mediaType = "application/json";
            _responder = null;
            return this;
        }

        public IHttpMockResponseBuilder WithHeader(string name, params string[] values)
        {
            foreach (var value in values)
            {
                _headers.Add(new KeyValuePair<string, string>(name, value));
            }
            return this;
        }

        public Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responder is not null)
            {
                return _responder(request, cancellationToken);
            }

            var response = new HttpResponseMessage(_statusCode);
            if (_body is not null)
            {
                response.Content = new StringContent(_body, Encoding.UTF8, _mediaType);
            }

            foreach (var header in _headers)
            {
                if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    response.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return Task.FromResult(response);
        }
    }

    private sealed class HttpMockHandler(HttpMock mock) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => mock.HandleAsync(request, cancellationToken);
    }
}
