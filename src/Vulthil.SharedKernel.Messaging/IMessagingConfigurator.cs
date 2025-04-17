using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Messaging;

public interface IMessagingConfigurator
{
    IServiceCollection Services { get; }

    IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction);
}
