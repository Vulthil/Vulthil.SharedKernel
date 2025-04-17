using Vulthil.SharedKernel.Messaging.Consumers;

namespace Vulthil.SharedKernel.Messaging;

public interface IQueueConfigurator
{
    IQueueConfigurator AddConsumer<TConsumer>() where TConsumer : class, IConsumer;
}
