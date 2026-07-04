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

internal sealed class InnerHandlerAdapter<TRequest, TResponse>(IHandler<TRequest, TResponse> handler)
    : IInnerHandler<TRequest, TResponse>, IDisposable, IAsyncDisposable
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default) =>
        handler.HandleAsync(request, cancellationToken);

    public void Dispose()
    {
        switch (handler)
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
        switch (handler)
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
