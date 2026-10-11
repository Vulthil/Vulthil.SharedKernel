using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Pipeline;

/// <summary>
/// Internal marker that resolves the concrete handler implementation registered for
/// (<typeparamref name="TRequest"/>, <typeparamref name="TResponse"/>) without exposing
/// the concrete type to consumers. The <see cref="PipelineHandlerDecorator{TRequest, TResponse}"/>
/// depends on this marker so that the only handler interfaces reachable from DI are the
/// pipeline-wrapped ones.
/// </summary>
internal interface IInnerHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates the concrete <typeparamref name="THandler"/> for the current scope and forwards to it.
/// </summary>
/// <remarks>
/// The handler is a type argument rather than a captured value, so the implementation type of a registration names
/// the handler it resolves. <see cref="HandlerRegistrar"/> relies on that to tell a repeated registration of the same
/// handler from a second, different handler for the same request.
/// </remarks>
internal sealed class InnerHandlerAdapter<TRequest, TResponse, THandler>(IServiceProvider serviceProvider)
    : IInnerHandler<TRequest, TResponse>, IDisposable, IAsyncDisposable
    where TRequest : IRequest<TResponse>
    where THandler : class, IHandler<TRequest, TResponse>
{
    private readonly THandler _handler = ActivatorUtilities.CreateInstance<THandler>(serviceProvider);

    public Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) =>
        _handler.HandleAsync(request, cancellationToken);

    public void Dispose()
    {
        switch (_handler)
        {
            case IDisposable disposable:
                disposable.Dispose();
                break;
            case IAsyncDisposable asyncDisposable:
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        switch (_handler)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
