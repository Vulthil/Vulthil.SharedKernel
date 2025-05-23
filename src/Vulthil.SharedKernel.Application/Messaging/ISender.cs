using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Messaging;
public interface ISender
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IHaveResponse<TResponse>;
}


internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlers = new();

    public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IHaveResponse<TResponse>
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = (RequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.HandleAsync(request, _serviceProvider, cancellationToken);
    }
}
internal class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IHaveResponse<TResponse>
{
    public override async Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await HandleAsync((ICommand<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<TResponse> HandleAsync(IHaveResponse<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        Task<TResponse> Handler(CancellationToken t = default) => serviceProvider.GetRequiredService<IHandler<TRequest, TResponse>>()
            .HandleAsync((TRequest)request, t);

        var pipeline = serviceProvider
            .GetServices<IPipelineHandler<TRequest, TResponse>>()
            .Reverse()
            .Aggregate((PipelineDelegate<TResponse>)Handler,
                (next, pipeline) => (t) => pipeline.HandleAsync((TRequest)request, next, t));

        return pipeline(cancellationToken);
    }
}
internal abstract class RequestHandlerBase
{
    public abstract Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    public abstract Task<TResponse> HandleAsync(IHaveResponse<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
