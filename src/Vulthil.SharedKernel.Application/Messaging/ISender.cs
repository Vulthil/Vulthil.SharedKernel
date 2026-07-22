using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Application.Messaging;

/// <summary>
/// Dispatches requests to their registered handlers through the pipeline.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request through the pipeline and returns the response.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task containing the response.</returns>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}


internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), object> _requestHandlers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = (IRequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd((request.GetType(), typeof(TResponse)), static key =>
        {
            var wrapperType = typeof(RequestHandlerWrapperResult<,>).MakeGenericType(key.RequestType, key.ResponseType);
            return Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {key.RequestType}");
        });

        return handler.HandleAsync(request, _serviceProvider, cancellationToken);
    }
}
internal class RequestHandlerWrapperResult<TRequest, TResponse> : IRequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        serviceProvider.GetRequiredService<IHandler<TRequest, TResponse>>()
            .HandleAsync((TRequest)request, cancellationToken);
}

internal interface IRequestHandlerWrapper<TResponse>
{
    Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
