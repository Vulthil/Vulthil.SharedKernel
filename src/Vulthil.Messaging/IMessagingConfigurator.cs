using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

public interface IMessagingConfigurator
{
    IHostApplicationBuilder HostApplicationBuilder { get; }
    IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction);

    void ConfigureMessagingOptions(Action<MessagingOptions> action);

    IMessagingConfigurator RegisterRoutingKeyFormatter<T>(Func<T, string> picker) where T : class;
    IMessagingConfigurator RegisterCorrelationIdFormatter<T>(Func<T, string> picker) where T : class;
}
