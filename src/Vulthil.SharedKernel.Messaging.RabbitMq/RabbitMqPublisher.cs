using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.SharedKernel.Messaging.Abstractions.Publishers;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

internal sealed class RabbitMqPublisher : IPublisher, IDisposable, IAsyncDisposable
{
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly IConnection _rabbitMqConnection;
    private readonly TypeCache _typeCache;
    private readonly Dictionary<Type, EventOption> _undeclaredEvents = [];
    private readonly SemaphoreSlim _channelSemaphore = new(1, 1);
    private IChannel? _channel;

    public RabbitMqPublisher(ILogger<RabbitMqPublisher> logger, IConnection rabbitMqConnection, TypeCache typeCache)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _typeCache = typeCache;
    }

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        _channel ??= await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);

        if (!_typeCache.TryGetEvent<TMessage>(out var eventOption) && !_undeclaredEvents.TryGetValue(typeof(TMessage), out eventOption))
        {
            eventOption = new EventOption(new(typeof(TMessage)));
            _undeclaredEvents.Add(typeof(TMessage), eventOption);
            _logger.LogInformation("Undeclared event published. Declaring default durable fanout exchange. {EventOption}", eventOption);
            await _channelSemaphore.WaitAsync(cancellationToken);
            try
            {

                await _channel.ExchangeDeclareAsync(eventOption.ExchangeName, eventOption.ExchangeType, eventOption.ExchangeDurable, eventOption.ExchangeAutoDelete, cancellationToken: cancellationToken);
            }
            finally
            {
                _channelSemaphore.Release();
            }
        }


        var properties = new BasicProperties()
        {
            Type = message.GetType().FullName,
            ContentType = RabbitMqConstants.ContentType,
            Headers = new Dictionary<string, object?>()
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, message.GetType()));
        try
        {
            await _channelSemaphore.WaitAsync(cancellationToken);

            await _channel.BasicPublishAsync(eventOption.ExchangeName, string.Empty, false, properties, body, cancellationToken);
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

    public ValueTask DisposeAsync()
    {

        _channelSemaphore.Dispose();

        Dispose(false);

        return ValueTask.CompletedTask;
    }

    #endregion
}
