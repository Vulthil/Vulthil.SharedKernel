using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Framework.Results;
using Vulthil.Framework.Results.Results;
using Vulthil.SharedKernel.Messaging.Abstractions.Consumers;
using Vulthil.SharedKernel.Messaging.Abstractions.Publishers;

namespace Vulthil.SharedKernel.Messaging.TestHarness;

public static class DependencyInjection
{
    public static IServiceCollection AddTestHarness(this IServiceCollection services, Action<TestTransportConfiguration>? testTransportConfigurationAction = null)
    {
        services.RemoveAll<ITransport>();
        services.RemoveAll<IPublisher>();
        services.RemoveAll<IRequester>();

        var testTransportConfiguration = new TestTransportConfiguration();
        testTransportConfigurationAction?.Invoke(testTransportConfiguration);
        services.AddSingleton(testTransportConfiguration);

        services.AddSingleton<TestTransport>();
        services.AddSingleton<ITestTransport>(sp => sp.GetRequiredService<TestTransport>());
        services.AddSingleton<ITransport>(sp => sp.GetRequiredService<TestTransport>());
        services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<TestTransport>());
        services.AddSingleton<IRequester>(sp => sp.GetRequiredService<TestTransport>());

        return services;
    }
}

public sealed class TestTransportConfiguration
{
    private readonly Dictionary<Type, Func<object, object>> _responses = [];

    public void AddResponse<TRequest, TResponse>(TResponse response)
        where TRequest : class
        where TResponse : class => _responses.Add(typeof(TRequest), (_) => response);
    public void AddResponse<TRequest, TResponse>(Func<TRequest, TResponse> responseFunction)
        where TRequest : class
        where TResponse : class => _responses.Add(typeof(TRequest), (o) => o is TRequest request ? responseFunction(request) : throw new ArgumentException($"Not {typeof(TRequest)}", nameof(o)));
    internal TResponse GetResponse<TRequest, TResponse>(TRequest message)
        where TRequest : class
        where TResponse : class
    {
        if (!_responses.TryGetValue(typeof(TRequest), out var responseFunction))
        {
            throw new ArgumentException($"{typeof(TRequest)} does not have a configured response.");
        }
        return (TResponse)responseFunction(message);
    }

}

internal class TestTransport : ITransport, IRequester, IPublisher, ITestTransport
{
    private readonly TestTransportConfiguration _testTransportConfiguration;
    private readonly IEnumerable<QueueDefinition> _queueDefinitions;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly List<object> _publishedMessages = [];

    public TestTransport(
        TestTransportConfiguration testTransportConfiguration,
        IEnumerable<QueueDefinition> queueDefinitions,
        IServiceScopeFactory serviceScopeFactory)
    {
        _testTransportConfiguration = testTransportConfiguration;
        _queueDefinitions = queueDefinitions;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class
    {
        _publishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        _publishedMessages.Add(message);

        try
        {
            return Task.FromResult(Result.Success(_testTransportConfiguration.GetResponse<TRequest, TResponse>(message)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<TResponse>(Error.NotFound("NotConfigured", ex.Message)));
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task ConsumeAsync<TMessage>(TMessage message) where TMessage : class
    {
        var consumers = _queueDefinitions.Where(q => q.Messages.ContainsKey(new MessageType(typeof(TMessage))))
            .SelectMany(q => q.Messages.TryGetValue(new MessageType(typeof(TMessage)), out var consumers) ? consumers : []);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        foreach (var consumerType in consumers)
        {
            var resolvedConsumer = scope.ServiceProvider.GetRequiredService(consumerType.Type);

            var consumeTask = resolvedConsumer switch
            {
                IConsumer consumer => consumer.ConsumeAsync(message),
                _ => throw new NotImplementedException()
            };
            await consumeTask;
        }
    }
}

public interface ITestTransport
{
    Task ConsumeAsync<TMessage>(TMessage message) where TMessage : class;
}
