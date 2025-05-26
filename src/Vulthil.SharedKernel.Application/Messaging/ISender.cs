using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Messaging;
public interface ISender
{
    Task<Result<TResponse>> SendAsync<TResponse>(IHaveResponse<TResponse> request, CancellationToken cancellationToken = default);
    Task<Result> SendAsync<TResponse>(IHaveResponse request, CancellationToken cancellationToken = default);
}


internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlers = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(IHaveResponse<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = (RequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapperResult<,>).MakeGenericType(requestType, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.HandleAsync(request, _serviceProvider, cancellationToken);
    }

    public Task<Result> SendAsync<TResponse>(IHaveResponse request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = (RequestHandlerWrapper)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapperResult<>).MakeGenericType(requestType);
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.HandleAsync(request, _serviceProvider, cancellationToken);
    }
}
internal class RequestHandlerWrapperResult<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IHaveResponse<TResponse>
{
    public override async Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        await HandleAsync((ICommand<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<Result<TResponse>> HandleAsync(IHaveResponse<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        Task<Result<TResponse>> Handler(CancellationToken t) => serviceProvider.GetRequiredService<IHandler<TRequest, TResponse>>()
            .HandleAsync((TRequest)request, t);

        var pipeline = serviceProvider
            .GetServices<IPipelineHandler<TRequest, TResponse>>()
            .Reverse()
            .Aggregate((PipelineDelegate<TResponse>)Handler,
                (next, pipeline) => (t) => pipeline.HandleAsync((TRequest)request, next, t));

        return pipeline(cancellationToken);
    }
}
internal class RequestHandlerWrapperResult<TRequest> : RequestHandlerWrapper
    where TRequest : IHaveResponse
{
    public override async Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        await HandleAsync((IHaveResponse)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<Result> HandleAsync(IHaveResponse request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        Task<Result> Handler(CancellationToken t) => serviceProvider.GetRequiredService<IHandler<TRequest>>()
                        .HandleAsync((TRequest)request, t);

        var pipeline = serviceProvider
            .GetServices<IPipelineHandler<TRequest>>()
            .Reverse()
            .Aggregate((PipelineDelegate)Handler,
                (next, pipeline) => (t) => pipeline.HandleAsync((TRequest)request, next, t));

        return pipeline(cancellationToken);
    }
}
internal abstract class RequestHandlerBase
{
    public abstract Task<object?> HandleAsync(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
internal abstract class RequestHandlerWrapper : RequestHandlerBase
{
    public abstract Task<Result> HandleAsync(IHaveResponse request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    public abstract Task<Result<TResponse>> HandleAsync(IHaveResponse<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
