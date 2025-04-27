using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

public sealed class RabbitMqConnectionFactory : IDisposable, IAsyncDisposable
{
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly IConnection _connection;
    private readonly ConnectionFactory _factory = new();
    private readonly List<IConnection> _connections = [];

    public RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options, IConnection connection)
    {
        _options = options;
        _connection = connection;
        _factory.Uri = new Uri(_options.Value.ConnectionString);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        return Task.FromResult(_connection);
        //var conn = await _factory.CreateConnectionAsync(cancellationToken);

        //_connections.Add(conn);
        //return conn;
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
