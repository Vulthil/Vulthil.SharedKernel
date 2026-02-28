using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

public interface IMessagingConfigurator
{
    IHostApplicationBuilder HostApplicationBuilder { get; }
    IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction);

    IMessagingConfigurator ConfigureMessagingOptions(Action<MessagingOptions> action);
    IMessagingConfigurator ConfigureFaults(Action<IFaultConfigurator> configureFaults);

    IMessagingConfigurator RegisterRoutingKeyFormatter<T>(Func<T, string> picker) where T : class;
    IMessagingConfigurator RegisterRoutingKeyFormatter<T>(string routingKey) where T : class;

    IMessagingConfigurator RegisterCorrelationIdFormatter<T>(Func<T, string> picker) where T : class;

}
public interface IFaultConfigurator
{
    /// <summary>
    /// Sets the name of the global topic exchange for faults. Default is "Fault.Exchange".
    /// </summary>
    void UseExchange(string exchangeName);

    /// <summary>
    /// Defines the local queue name that will receive faults. 
    /// If not set, a default name (e.g., "service-name.faults") is used.
    /// </summary>
    void UseQueue(string queueName);

    /// <summary>
    /// Adds a consumer to handle faults received by this service.
    /// </summary>
    void AddConsumer<TConsumer>() where TConsumer : class, IConsumer;

    /// <summary>
    /// Sets the routing keys to listen for (e.g. "#", "Order.*"). Default is "#".
    /// </summary>
    void Bind(params string[] routingKeys);
}
