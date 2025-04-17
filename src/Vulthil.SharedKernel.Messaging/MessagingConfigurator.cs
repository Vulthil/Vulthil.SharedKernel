using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Messaging;

internal sealed class MessagingConfigurator : IMessagingConfigurator
{
    private readonly Dictionary<string, QueueConfigurator> _queues = [];

    public IServiceCollection Services { get; }
    private readonly TypeCache _typeCache = new();

    public MessagingConfigurator(IServiceCollection services)
    {
        Services = services;
        Services.AddSingleton(_typeCache);
    }

    public IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction)
    {
        var queueConfigurator = new QueueConfigurator(queueName, Services, _typeCache);
        queueConfigurationAction(queueConfigurator);
        _queues.Add(queueName, queueConfigurator);
        queueConfigurator.Register();
        return this;
    }
}
