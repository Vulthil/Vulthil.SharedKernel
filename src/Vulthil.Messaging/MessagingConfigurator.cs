using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

internal sealed class MessagingConfigurator : IMessagingConfigurator
{
    private const string DefaultSectionName = "Messaging";

    private readonly HashSet<QueueDefinition> _queues = [];

    private readonly MessagingOptions _messagingOptions;

    public IHostApplicationBuilder HostApplicationBuilder { get; }
    private IServiceCollection _services => HostApplicationBuilder.Services;
    private IConfiguration _configuration => HostApplicationBuilder.Configuration;

    public MessagingConfigurator(IHostApplicationBuilder hostApplicationBuilder, MessagingOptions messagingOptions)
    {
        HostApplicationBuilder = hostApplicationBuilder;
        _messagingOptions = messagingOptions;
    }

    public void ConfigureMessagingOptions(Action<MessagingOptions> action) => action(_messagingOptions);

    private static string ConstructSectionName(string queueName) => $"{DefaultSectionName}:Queues:{queueName}";

    public IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        var queueDefinition = new QueueDefinition(queueName);
        _configuration.GetSection(ConstructSectionName(queueName)).Bind(queueDefinition);
        var queueConfigurator = new QueueConfigurator(_services, _messagingOptions, queueDefinition);

        queueConfigurationAction(queueConfigurator);
        _queues.Add(queueDefinition);
        _services.AddSingleton(queueDefinition);

        return this;
    }

    public IMessagingConfigurator RegisterRoutingKeyFormatter<T>(string routingKey) where T : class
        => RegisterRoutingKeyFormatter<T>(_ => routingKey);

    public IMessagingConfigurator RegisterRoutingKeyFormatter<T>(Func<T, string> picker) where T : class
    {
        _messagingOptions.RoutingKeyFormatters[typeof(T)] = (msg) => picker((T)msg);
        return this;
    }

    public IMessagingConfigurator RegisterCorrelationIdFormatter<T>(Func<T, string> picker) where T : class
    {
        _messagingOptions.CorrelationIdFormatters[typeof(T)] = (msg) => picker((T)msg);
        return this;
    }
}
