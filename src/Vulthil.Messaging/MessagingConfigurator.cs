using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.Messaging;

internal sealed class MessagingConfigurator : IMessagingConfigurator
{
    private readonly HashSet<QueueConfigurator> _queues = [];

    public IServiceCollection Services { get; }
    private readonly TypeCache _typeCache = new();
    private readonly IConfiguration _configuration;

    public MessagingConfigurator(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        _configuration = configuration;
        Services.AddSingleton(_typeCache);
    }

    public IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction)
    {
        var queueConfigurator = new QueueConfigurator(queueName);
        _configuration.GetSection(queueName).Bind(queueConfigurator);
        queueConfigurationAction(queueConfigurator);
        _queues.Add(queueConfigurator);
        return this;
    }

    public IMessagingConfigurator AddRequest<TRequest>(string queueName, Action<RequestOption>? requestOptionAction = null)
        where TRequest : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        var requestOption = new RequestOption
        {
            RecipientQueueName = queueName,
        };
        requestOptionAction?.Invoke(requestOption);
        _typeCache.AddRequestOption<TRequest>(requestOption);
        return this;
    }

    public IMessagingConfigurator AddEvent<TEvent>(Action<EventOption>? eventOptionAction = null)
        where TEvent : notnull
    {
        var messageType = new MessageType(typeof(TEvent));
        var eventOption = new EventOption(messageType);
        eventOptionAction?.Invoke(eventOption);
        _typeCache.AddEventOption<TEvent>(eventOption);
        return this;
    }

    internal void RegisterTypes()
    {
        foreach (var queue in _queues)
        {
            var queueDefinition = queue.ToQueueDefinition();
            foreach (var item in queueDefinition.Messages.Keys)
            {
                _typeCache.AddTypeMap(item);
            }

            Services.AddSingleton(queueDefinition);
        }
    }
}
