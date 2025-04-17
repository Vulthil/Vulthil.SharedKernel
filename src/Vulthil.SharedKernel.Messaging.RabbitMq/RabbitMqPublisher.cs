using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Vulthil.SharedKernel.Messaging.Publishers;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

internal sealed class RabbitMqPublisher : IPublisher, IDisposable, IAsyncDisposable
{
    private const string ApplicationJson = "application/json";

    private readonly RabbitMqConnectionFactory _rabbitMqConnectionFactory;
    private readonly SemaphoreSlim _channelSemaphore = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(RabbitMqConnectionFactory rabbitMqConnectionFactory) => _rabbitMqConnectionFactory = rabbitMqConnectionFactory;

    private static BasicProperties CreateBasicProperties<TMessage>(TMessage message)
        where TMessage : class =>
        new()
        {
            Type = message.GetType().FullName!,
            ContentType = ApplicationJson,
        };

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        _connection ??= await _rabbitMqConnectionFactory.CreateConnectionAsync(cancellationToken);
        _channel ??= await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var properties = CreateBasicProperties(message);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, message.GetType()));
        try
        {
            await _channelSemaphore.WaitAsync(cancellationToken);

            await _channel.BasicPublishAsync(properties.Type!, string.Empty, false, properties, body, cancellationToken);
        }
        finally
        {
            _channelSemaphore.Release();
        }
    }

    #region Dispose

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _channelSemaphore.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {

        _channelSemaphore.Dispose();
        await (_connection?.CloseAsync() ?? Task.CompletedTask);

        Dispose(false);
    }

    #endregion
}
