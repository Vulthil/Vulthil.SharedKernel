using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.Create;

namespace WebApi.Application.MainEntities.GetById;

/// <summary>
/// Represents the GetMainEntities.
/// </summary>
public sealed record GetMainEntities : IQuery<Result<IReadOnlyList<MainEntityDto>>>;

/// <summary>
/// Represents the GetMainEntitiesQueryHandler.
/// </summary>
public sealed class GetMainEntitiesQueryHandler(ILogger<GetMainEntitiesQueryHandler> logger, IWebApiDbContext dbContext, IRequester requester) : IQueryHandler<GetMainEntities, Result<IReadOnlyList<MainEntityDto>>>
{
    private readonly ILogger<GetMainEntitiesQueryHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;
    private readonly IRequester _requester = requester;

    /// <summary>
    /// Executes this member.
    /// </summary>
    public async Task<Result<IReadOnlyList<MainEntityDto>>> HandleAsync(GetMainEntities query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying all MainEntities");
        var list = await _dbContext.MainEntities.ToListAsync(cancellationToken);

        var result = await Task.WhenAll(list.Select(entity =>
                _requester.RequestAsync<GetSideEffectsBelongingToMainEntity, List<SideEffectDto>>(new GetSideEffectsBelongingToMainEntity(entity.Id.Value), null, cancellationToken)
                .MapAsync(sideEffects => (entity, sideEffects))
                .MapAsync(tuple => MainEntityDto.FromModel(tuple.entity, tuple.sideEffects))));

        return result.Where(x => x.IsSuccess).Select(x => x.Value).ToList();

    }
}
