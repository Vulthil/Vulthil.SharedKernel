using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.SideEffects;

namespace WebApi.Application.SideEffects.GetById;

public sealed record GetSideEffectQuery(Guid Id) : IQuery<Result<SideEffectDto>>;

internal sealed class GetSideEffectQueryHandler(IWebApiDbContext dbContext) : IQueryHandler<GetSideEffectQuery, Result<SideEffectDto>>
{
    public Task<Result<SideEffectDto>> HandleAsync(GetSideEffectQuery request, CancellationToken cancellationToken = default)
    {
        var id = new SideEffectId(request.Id);
        return dbContext.SideEffects
            .AsNoTracking()
            .FirstOrDefaultAsync(sideEffect => sideEffect.Id == id, cancellationToken)
            .ToResultAsync(SideEffectErrors.NotFound(request.Id))
            .MapAsync(SideEffectDto.FromModel);
    }
}
