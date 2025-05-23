namespace Vulthil.SharedKernel.Application.Pipeline;

public delegate Task<TResponse> PipelineDelegate<TResponse>(CancellationToken cancellationToken = default);
public interface IPipelineHandler<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default);
}

