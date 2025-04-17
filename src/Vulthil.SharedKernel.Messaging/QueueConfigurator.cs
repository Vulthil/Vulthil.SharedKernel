using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.Messaging.Consumers;

namespace Vulthil.SharedKernel.Messaging;

internal sealed class QueueConfigurator : IQueueConfigurator
{
    private readonly Dictionary<Type, List<Type>> _consumers = [];
    private readonly TypeCache _typeCache;

    public string QueueName { get; }
    public IServiceCollection Services { get; }

    public QueueConfigurator(string queueName, IServiceCollection services, TypeCache typeCache)
    {
        QueueName = queueName;
        Services = services;
        _typeCache = typeCache;
    }

    internal void Register()
    {
        Services.AddSingleton(new QueueDefinition(QueueName, _consumers));
    }

    public IQueueConfigurator AddConsumer<TConsumer>()
       where TConsumer : class, IConsumer
    {

        var consumerType = typeof(TConsumer);
        var types = Enumerable.Empty<Type>();
        if (consumerType.IsGenericType && consumerType.GetGenericTypeDefinition() == typeof(IConsumer<>))
        {
            types = types.Concat([consumerType.GetGenericArguments()[0]]);
        }

        var typeList = types.Concat(consumerType.GetInterfaces()
            .Where(x => x.IsGenericType)
            .Where(x => x.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(x => x.GetGenericArguments()[0]))
            .Distinct()
            .ToList();

        Services.TryAddScoped<TConsumer>();
        _consumers.Add(typeof(TConsumer), typeList);
        typeList.ForEach(_typeCache.AddTypeMap);

        return this;
    }
}
