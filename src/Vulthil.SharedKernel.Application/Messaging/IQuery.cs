namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQuery<out TResponse> : IHaveResponse<TResponse>;
