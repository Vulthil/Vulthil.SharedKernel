namespace Vulthil.Messaging;

public interface ITransport
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
