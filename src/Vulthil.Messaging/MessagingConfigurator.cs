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

    /// <inheritdoc />
    public IHostApplicationBuilder HostApplicationBuilder { get; }
    private IServiceCollection _services => HostApplicationBuilder.Services;
    private IConfiguration _configuration => HostApplicationBuilder.Configuration;

    /// <inheritdoc />
    public MessagingConfigurator(IHostApplicationBuilder hostApplicationBuilder, MessagingOptions messagingOptions)
    {
        HostApplicationBuilder = hostApplicationBuilder;
        _messagingOptions = messagingOptions;
        _services.AddSingleton<IMessageConfigurationProvider>(_ => new MessageConfigurationProvider(_messagingOptions));
    }

    public IMessagingConfigurator ConfigureMessagingOptions(Action<MessagingOptions> action)
    {
        action(_messagingOptions);
        return this;
    }

    public IMessagingConfigurator ConfigureFaults(Action<IFaultConfigurator> configureFaults) => throw new NotImplementedException();

    private static string ConstructQueueSectionName(string queueName) => $"{DefaultSectionName}:Queues:{queueName}";

    public IMessagingConfigurator ConfigureQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        var queueDefinition = new QueueDefinition(queueName);
        _configuration.GetSection(ConstructQueueSectionName(queueName)).Bind(queueDefinition);
        var queueConfigurator = new QueueConfigurator(_services, _messagingOptions, queueDefinition);

        queueConfigurationAction(queueConfigurator);
        _queues.Add(queueDefinition);
        _services.AddSingleton(queueDefinition);

        return this;
    }

    private static string ConstructMessageSectionName<TMessage>() => $"{DefaultSectionName}:Messages:{typeof(TMessage).FullName}";

    public IMessagingConfigurator ConfigureMessage<TMessage>(Action<MessageConfiguration<TMessage>> configureMessageAction)
        where TMessage : class
    {
        var messageConfiguration = new MessageConfiguration<TMessage>();
        _configuration.GetSection(ConstructMessageSectionName<TMessage>()).Bind(messageConfiguration);

        configureMessageAction(messageConfiguration);
        _messagingOptions.MessageConfigurations[typeof(TMessage)] = messageConfiguration;

        return this;
    }
}
