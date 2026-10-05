using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// A transport's raw send-endpoint provider. The public <see cref="ISendEndpointProvider"/> registered to callers
/// is a filtering facade that wraps the returned <see cref="ITransportSendEndpoint"/> with the publish pipeline.
/// Transports register their provider under this interface instead of <see cref="ISendEndpointProvider"/>.
/// </summary>
public interface ITransportSendEndpointProvider
{
    /// <summary>Resolves the transport's send endpoint for <paramref name="address"/>.</summary>
    /// <param name="address">The destination endpoint address.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The endpoint bound to <paramref name="address"/>. Implementations may reject an address they cannot route to
    /// here, and may cache and share endpoints across calls.
    /// </returns>
    ValueTask<ITransportSendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default);
}
