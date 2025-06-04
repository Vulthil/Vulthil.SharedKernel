using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Messaging;
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}


internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private static readonly ConcurrentDictionary<Type, IRequestHandlerBase> _requestHandlers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = (IRequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapperResult<,>).MakeGenericType(requestType, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (IRequestHandlerBase)wrapper;
        });

        return handler.HandleAsync(request, _serviceProvider, cancellationToken);
    }
}
internal class RequestHandlerWrapperResult<TRequest, TResponse> : IRequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        await HandleAsync((ICommand<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        Task<TResponse> Handler(CancellationToken t) => serviceProvider.GetRequiredService<IHandler<TRequest, TResponse>>()
            .HandleAsync((TRequest)request, t);

        var pipeline = serviceProvider
            .GetServices<IPipelineHandler<TRequest, TResponse>>()
            .Reverse()
            .Aggregate((PipelineDelegate<TResponse>)Handler,
                (next, pipeline) => (t) => pipeline.HandleAsync((TRequest)request, next, t));

        return pipeline(cancellationToken);
    }
}

internal interface IRequestHandlerBase
{
    Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}

internal interface IRequestHandlerWrapper<TResponse> : IRequestHandlerBase
{
    Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
