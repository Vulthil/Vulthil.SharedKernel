using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

internal sealed class MessagingConfigurator : IMessagingConfigurator
{
    internal const string DefaultSectionName = "Messaging";

    private readonly MessagingOptions _messagingOptions;

    /// <inheritdoc />
    public IHostApplicationBuilder HostApplicationBuilder { get; }
    private IServiceCollection Services => HostApplicationBuilder.Services;
    private IConfiguration Configuration => HostApplicationBuilder.Configuration;

    /// <inheritdoc />
    public MessagingConfigurator(IHostApplicationBuilder hostApplicationBuilder, MessagingOptions messagingOptions)
    {
        HostApplicationBuilder = hostApplicationBuilder;
        _messagingOptions = messagingOptions;
        Services.AddSingleton<IMessageConfigurationProvider>(_messagingOptions);
    }

    public IMessagingConfigurator ConfigureMessagingOptions(Action<IMessagingOptionsConfigurator> action)
    {
        action(_messagingOptions);
        return this;
    }

    internal static string ConstructQueueSectionName(string queueName) => $"{DefaultSectionName}:Queues:{queueName}";

    public IMessagingConfigurator ConfigureQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        if (!_messagingOptions.QueueDefinitions.TryGetValue(queueName, out var queueDefinition))
        {
            queueDefinition = new QueueDefinition(queueName);
            Configuration.GetSection(ConstructQueueSectionName(queueName)).Bind(queueDefinition);
            _messagingOptions.QueueDefinitions[queueName] = queueDefinition;
        }

        var queueConfigurator = new QueueConfigurator(Services, _messagingOptions, queueDefinition);
        queueConfigurationAction(queueConfigurator);

        return this;
    }

    internal static string ConstructMessageSectionName(string fullName) => $"{DefaultSectionName}:Messages:{fullName}";

    public IMessagingConfigurator ConfigureMessage<TMessage>(Action<MessageConfiguration<TMessage>> configureMessageAction)
        where TMessage : class
    {
        var fullName = typeof(TMessage).FullName
            ?? throw new InvalidOperationException($"Cannot derive a message configuration key for type '{typeof(TMessage)}'.");

        MessageConfiguration<TMessage> typed;

        if (_messagingOptions.MessageConfigurations.TryGetValue(fullName, out var existing))
        {
            if (existing is MessageConfiguration<TMessage> alreadyTyped)
            {
                typed = alreadyTyped;
            }
            else
            {
                typed = new MessageConfiguration<TMessage>();
                CopyMessageConfiguration(existing, typed);
                _messagingOptions.MessageConfigurations[fullName] = typed;
            }
        }
        else
        {
            typed = new MessageConfiguration<TMessage>();
            Configuration.GetSection(ConstructMessageSectionName(fullName)).Bind(typed);
            _messagingOptions.MessageConfigurations[fullName] = typed;
        }

        configureMessageAction(typed);
        return this;
    }

    private static void CopyMessageConfiguration(MessageConfiguration src, MessageConfiguration dst)
    {
        dst.Exchange = src.Exchange;
        dst.ExchangeType = src.ExchangeType;
        dst.Durable = src.Durable;
        dst.AutoDelete = src.AutoDelete;
        foreach (var kvp in src.Arguments)
        {
            dst.Arguments[kvp.Key] = kvp.Value;
        }
        dst.RoutingKeyFormatter = src.RoutingKeyFormatter;
        dst.CorrelationIdFormatter = src.CorrelationIdFormatter;
    }

    public IMessagingConfigurator AddConsumeFilter<TFilter>() where TFilter : class
    {
        var filterType = typeof(TFilter);
        var filterInterfaces = filterType.GetInterfaces()
            .Where(i => i.IsGenericType && !i.IsGenericTypeDefinition && i.GetGenericTypeDefinition() == typeof(IConsumeFilter<>))
            .ToList();

        if (filterInterfaces.Count == 0)
        {
            throw new InvalidOperationException(
                $"'{filterType.FullName}' must implement at least one '{typeof(IConsumeFilter<>).FullName}' interface to be registered as a consume filter.");
        }

        foreach (var iface in filterInterfaces)
        {
            Services.TryAddEnumerable(new ServiceDescriptor(iface, filterType, ServiceLifetime.Scoped));
        }

        return this;
    }

    public IMessagingConfigurator AddOpenConsumeFilter(Type openFilterType)
    {
        ArgumentNullException.ThrowIfNull(openFilterType);

        if (!openFilterType.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException(
                $"'{openFilterType.FullName}' must be an open generic type (e.g. typeof(MyFilter<>)) to be registered as an open consume filter.");
        }

        var implementsFilter = false;
        foreach (var iface in openFilterType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IConsumeFilter<>))
            {
                implementsFilter = true;
                break;
            }
        }

        if (!implementsFilter)
        {
            throw new InvalidOperationException(
                $"'{openFilterType.FullName}' must implement '{typeof(IConsumeFilter<>).FullName}' to be registered as an open consume filter.");
        }

        Services.TryAddEnumerable(new ServiceDescriptor(typeof(IConsumeFilter<>), openFilterType, ServiceLifetime.Scoped));
        return this;
    }
}
