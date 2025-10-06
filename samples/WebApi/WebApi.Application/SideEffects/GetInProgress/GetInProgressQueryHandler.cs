using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.SideEffects;

namespace WebApi.Application.SideEffects.GetInProgress;

public sealed record GetInProgressQuery : IQuery<Result<List<SideEffectDto>>>;

public sealed record SideEffectDto
{
    public Guid Id { get; init; }
    public Guid MainEntityId { get; init; }
    public StatusEnum Status { get; init; }

    public static SideEffectDto FromModel(SideEffect sideEffect) => new()
    {
        Id = sideEffect.Id.Value,
        MainEntityId = sideEffect.MainEntityId,
        Status = sideEffect.Status.ToDto()
    };
}

public enum StatusEnum
{
    InProgress,
    Completed,
    Failed
}

public static class StatusExtensions
{
    public static StatusEnum ToDto(this IStatus status) => status switch
    {
        InProgressStatus => StatusEnum.InProgress,
        CompletedStatus => StatusEnum.Completed,
        _ => StatusEnum.Failed
    };
}


internal class GetInProgressQueryHandler(IWebApiDbContext webApiDbContext) : IQueryHandler<GetInProgressQuery, Result<List<SideEffectDto>>>
{
    private readonly IWebApiDbContext _webApiDbContext = webApiDbContext;

    public async Task<Result<List<SideEffectDto>>> HandleAsync(GetInProgressQuery request, CancellationToken cancellationToken = default)
    {
        var list = await _webApiDbContext.SideEffects
            .Where(s => ((string)(object)s.Status).StartsWith("I"))
            .ToListAsync(cancellationToken);

        return list
            .ToResult(Error.None)
            .Map(s => s.Select(SideEffectDto.FromModel).ToList());
    }
}
