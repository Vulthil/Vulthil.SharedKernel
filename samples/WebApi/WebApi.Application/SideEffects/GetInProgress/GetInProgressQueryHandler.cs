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
    public required Status Status { get; init; }

    public static SideEffectDto FromModel(SideEffect sideEffect) => new()
    {
        Id = sideEffect.Id.Value,
        MainEntityId = sideEffect.MainEntityId,
        Status = sideEffect.Status
    };
}

internal class GetInProgressQueryHandler(IWebApiDbContext webApiDbContext) : IQueryHandler<GetInProgressQuery, Result<List<SideEffectDto>>>
{
    private readonly IWebApiDbContext _webApiDbContext = webApiDbContext;

    public async Task<Result<List<SideEffectDto>>> HandleAsync(GetInProgressQuery request, CancellationToken cancellationToken = default)
    {
        var list = await _webApiDbContext.SideEffects
            //.Where(s => s.Status is Status.InProgressStatus)
            .Where(s => EF.Functions.Like((string)(object)s.Status, "I %"))
            .ToListAsync(cancellationToken);

        return list
            .Select(SideEffectDto.FromModel)
            .ToList()
            .ToResult(Error.None);
    }
}
