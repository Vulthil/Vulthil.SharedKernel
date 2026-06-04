using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// A <see cref="PublishContext"/> for request/reply: adds the per-request timeout. A transport creates one,
/// passes it to the caller's <c>configure</c> callback as <see cref="IRequestContext"/>, then reads
/// <see cref="Timeout"/> to bound the wait for a response.
/// </summary>
public sealed class RequestContext : PublishContext, IRequestContext
{
    /// <summary>Gets the per-request timeout, or <see langword="null"/> to use the transport default.</summary>
    public TimeSpan? Timeout { get; private set; }

    /// <inheritdoc />
    public void SetTimeout(TimeSpan timeout) => Timeout = timeout;
}
