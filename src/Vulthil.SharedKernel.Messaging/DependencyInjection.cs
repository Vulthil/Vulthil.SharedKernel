using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, Action<IMessagingConfigurator> messagingConfiguratorAction)
    {
        var messagingConfigurator = new MessagingConfigurator(services);

        messagingConfiguratorAction(messagingConfigurator);

        return services;
    }
}
