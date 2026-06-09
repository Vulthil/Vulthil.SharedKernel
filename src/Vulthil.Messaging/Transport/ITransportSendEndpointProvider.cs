using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// A transport's raw send-endpoint provider. The public <see cref="ISendEndpointProvider"/> registered to callers
/// is a filtering facade that wraps the returned <see cref="ISendEndpoint"/> with the publish pipeline. Transports
/// register their provider under this interface instead of <see cref="ISendEndpointProvider"/>.
/// </summary>
public interface ITransportSendEndpointProvider
{
    /// <summary>Resolves the transport's send endpoint for <paramref name="address"/>.</summary>
    /// <param name="address">The destination endpoint address.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default);
}
