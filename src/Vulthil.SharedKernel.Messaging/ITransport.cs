namespace Vulthil.SharedKernel.Messaging;

public interface ITransport
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
