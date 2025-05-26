using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Pipeline;

public delegate Task<Result<TResponse>> PipelineDelegate<TResponse>(CancellationToken cancellationToken = default);
public delegate Task<Result> PipelineDelegate(CancellationToken cancellationToken = default);
public interface IPipelineHandler<in TRequest, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default);
}
public interface IPipelineHandler<in TRequest>
{
    Task<Result> HandleAsync(TRequest request, PipelineDelegate next, CancellationToken cancellationToken = default);
}

