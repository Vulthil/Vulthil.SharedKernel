using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

public sealed class RabbitMqConnectionFactory : IDisposable, IAsyncDisposable
{
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly ConnectionFactory _factory = new();
    private readonly List<IConnection> _connections = [];

    public RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
    {
        _options = options;
        _factory.Uri = new Uri(_options.Value.ConnectionString);
    }

    public async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var conn = await _factory.CreateConnectionAsync(cancellationToken);

        _connections.Add(conn);
        return conn;
    }

    #region Dispose

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connections.ForEach(x => x.Dispose());
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(_connections.Select(conn => conn.CloseAsync()));
        await Task.WhenAll(_connections.Select(conn => conn.DisposeAsync().AsTask()));

        Dispose(false);
    }

    #endregion
}
