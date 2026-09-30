namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// The <see cref="Vulthil.Results.Error.Code"/> values a request made through <see cref="IRequester"/> fails with, on
/// every transport. Match a failed result against these instead of the literal strings.
/// </summary>
public static class RequestErrorCodes
{
    /// <summary>
    /// No reply arrived within the request timeout. On the in-memory test harness it also means nothing handles the
    /// request type.
    /// </summary>
    public const string Timeout = "Messaging.Request.Timeout";

    /// <summary>The caller's cancellation token ended the request before a reply arrived.</summary>
    public const string Cancelled = "Messaging.Request.Cancelled";

    /// <summary>The transport failed to start, so the request was never sent.</summary>
    public const string TransportUnavailable = "Messaging.Request.TransportUnavailable";

    /// <summary>Sending the request failed.</summary>
    public const string Publish = "Messaging.Request.Publish";

    /// <summary>The reply could not be read: it was malformed, empty, or of an unexpected message type.</summary>
    public const string Deserialize = "Messaging.Request.Deserialize";

    /// <summary>The request consumer failed; the error's description carries the remote failure's message.</summary>
    public const string Failure = "Messaging.Request.Failure";
}
