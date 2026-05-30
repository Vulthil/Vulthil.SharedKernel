namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// Extends <see cref="IPublishContext"/> with request-specific configuration options, such as timeouts.
/// </summary>
public interface IRequestContext : IPublishContext
{
    /// <summary>
    /// Sets the timeout for the request, after which it should be considered failed if no response is received.
    /// </summary>
    IRequestContext SetTimeout(TimeSpan timeout);

}
