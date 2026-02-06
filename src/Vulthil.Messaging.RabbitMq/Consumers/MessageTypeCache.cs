using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal delegate Task MessageHandlerDelegate(
    IServiceProvider provider,
    object message,
    IMessageContext context,
    CancellationToken ct);

internal delegate Task<object> RpcHandlerDelegate(
    IServiceProvider provider,
    object message,
    IMessageContext context,
    CancellationToken ct);

internal static class ExpressionHelpers
{
    public static async Task<object> CastTask<T>(Task<T> task) where T : notnull
    {
        var result = await task;
        return result;
    }
}
internal sealed record MessageExecutionPlan(MessageType MessageType)
{
    public List<MessageHandlerDelegate> StandardHandlers { get; } = [];
    public RpcHandlerDelegate? RpcHandler { get; set; }
}
internal sealed class MessageTypeCache
{
    private readonly Dictionary<string, MessageExecutionPlan> _plans = [];
    public void RegisterQueue(QueueDefinition queue)
    {
        foreach (var consumer in queue.Registrations.OfType<ConsumerRegistration>())
        {
            var msgType = consumer.MessageType;
            var plan = GetOrAddPlan(msgType.Name, msgType);
            plan.StandardHandlers.Add(CompileHandler(consumer.ConsumerType, msgType));
        }

        // 2. Process RPC
        foreach (var rpc in queue.Registrations.OfType<RequestConsumerRegistration>())
        {
            var msgType = rpc.MessageType;
            var plan = GetOrAddPlan(msgType.Name, msgType);
            plan.RpcHandler = CompileRpcHandler(rpc.ConsumerType, msgType, rpc.ResponseType);


        }
    }

    private MessageExecutionPlan GetOrAddPlan(string name, MessageType type)
    {
        if (!_plans.TryGetValue(name, out var plan))
        {
            plan = new MessageExecutionPlan(type);
            _plans[name] = plan;
        }
        return plan;
    }
    public MessageExecutionPlan? GetPlan(string key) => _plans.GetValueOrDefault(key);

    private static MessageHandlerDelegate CompileHandler(ConsumerType consumerType, MessageType messageType)
    {
        // Create parameters for the lambda: (IServiceProvider, object, IMessageContext, CancellationToken)
        var spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
        var msgParam = Expression.Parameter(typeof(object), "msg");
        var ctxParam = Expression.Parameter(typeof(IMessageContext), "ctx");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        // 1. Resolve Consumer: sp.GetRequiredService(consumerType)
        var getServiceMethod = typeof(ServiceProviderServiceExtensions)
            .GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), [typeof(IServiceProvider), typeof(Type)])!;
        var resolveConsumer = Expression.Call(null, getServiceMethod, spParam, Expression.Constant(consumerType));
        var castConsumer = Expression.Convert(resolveConsumer, consumerType.Type);

        // 2. Create Context: new MessageContext<T>( (T)msg, ctx )
        var contextType = typeof(MessageContextImplementation<>).MakeGenericType(messageType.Type);
        var contextCtor = contextType.GetConstructor([messageType.Type, typeof(IMessageContext)])!;
        var castMsg = Expression.Convert(msgParam, messageType.Type);
        var createContext = Expression.New(contextCtor, castMsg, ctxParam);

        // 3. Call: consumer.ConsumeAsync(context, ct)
        // We target the specific typed interface method
        var interfaceType = typeof(IConsumer<>).MakeGenericType(messageType.Type);
        var method = interfaceType.GetMethod(nameof(IConsumer<>.ConsumeAsync), [typeof(IMessageContext<>).MakeGenericType(messageType.Type), typeof(CancellationToken)])!;
        var call = Expression.Call(castConsumer, method, createContext, ctParam);

        return Expression.Lambda<MessageHandlerDelegate>(call, spParam, msgParam, ctxParam, ctParam).Compile();
    }

    private static RpcHandlerDelegate CompileRpcHandler(ConsumerType consumerType, MessageType requestType, Type responseType)
    {
        var spParam = Expression.Parameter(typeof(IServiceProvider), "sp");
        var msgParam = Expression.Parameter(typeof(object), "msg");
        var ctxParam = Expression.Parameter(typeof(IMessageContext), "ctx");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        // 1. Resolve Consumer: (ConsumerType)sp.GetRequiredService(consumerType)
        var getServiceMethod = typeof(ServiceProviderServiceExtensions)
            .GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), [typeof(IServiceProvider), typeof(Type)])!;
        var resolveConsumer = Expression.Convert(
            Expression.Call(null, getServiceMethod, spParam, Expression.Constant(consumerType)),
            consumerType.Type);

        // 2. Create Context: new MessageContextImplementation<TRequest>((TRequest)msg, ctx)
        var contextType = typeof(MessageContextImplementation<>).MakeGenericType(requestType.Type);
        var contextCtor = contextType.GetConstructor([requestType.Type, typeof(IMessageContext)])!;
        var createContext = Expression.New(contextCtor, Expression.Convert(msgParam, requestType.Type), ctxParam);

        // 3. Call: consumer.ConsumeAsync(context, ct)
        // We target IRequestConsumer<TRequest, TResponse>.ConsumeAsync
        var interfaceType = typeof(IRequestConsumer<,>).MakeGenericType(requestType.Type, responseType);
        var method = interfaceType.GetMethod(nameof(IRequestConsumer<,>.ConsumeAsync),
            [typeof(IMessageContext<>).MakeGenericType(requestType.Type), typeof(CancellationToken)])!;

        var call = Expression.Call(resolveConsumer, method, createContext, ctParam);

        // 4. Convert Task<TResponse> to Task<object>
        // We call a small helper method to handle the async boxing efficiently
        var taskHelper = typeof(ExpressionHelpers)
            .GetMethod(nameof(ExpressionHelpers.CastTask), [responseType])!;

        var finalExpression = Expression.Call(null, taskHelper, call);

        return Expression.Lambda<RpcHandlerDelegate>(finalExpression, spParam, msgParam, ctxParam, ctParam).Compile();
    }
}
