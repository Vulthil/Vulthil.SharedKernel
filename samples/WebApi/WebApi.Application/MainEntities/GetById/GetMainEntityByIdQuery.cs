using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.Create;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.MainEntities.GetById;

public sealed record GetMainEntityByIdQuery(Guid Id) : IQuery<Result<MainEntityDto>>;
public sealed record MainEntityDto
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public required List<SideEffectDto> SideEffects { get; init; }

    private MainEntityDto(Guid id, string name) => (Id, Name) = (id, name);
    internal static MainEntityDto FromModel(MainEntity mainEntity, List<SideEffectDto> sideEffectDtos) =>
        new(mainEntity.Id.Value, mainEntity.Name)
        {
            SideEffects = sideEffectDtos
        };
}

/// <summary>
/// Loads the entity and asks for its side effects at the same time, then joins the two results with <c>Zip</c>: the
/// query fails with the first failure of the two. A side-effect request that times out is not a failure of the
/// query: <c>OrElse</c> turns that one error into an empty list.
/// </summary>
public sealed class GetMainEntityQueryHandler(ILogger<GetMainEntityQueryHandler> logger, IWebApiDbContext dbContext, IRequester requester) : IQueryHandler<GetMainEntityByIdQuery, Result<MainEntityDto>>
{
    private static readonly TimeSpan SideEffectsTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<GetMainEntityQueryHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;
    private readonly IRequester _requester = requester;

    public async Task<Result<MainEntityDto>> HandleAsync(GetMainEntityByIdQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying MainEntity With Id: {Id}", query.Id);

        var id = new MainEntityId(query.Id);
        var mainEntityTask = _dbContext.MainEntities
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            .ToResultAsync(MainEntityErrors.NotFound(query.Id));
        var sideEffectsTask = GetSideEffectsAsync(query.Id, cancellationToken);

        var mainEntity = await mainEntityTask;
        var sideEffects = await sideEffectsTask;

        return mainEntity.Zip(sideEffects, MainEntityDto.FromModel);
    }

    private async Task<Result<List<SideEffectDto>>> GetSideEffectsAsync(Guid mainEntityId, CancellationToken cancellationToken)
    {
        var sideEffects = await _requester.RequestAsync<GetSideEffectsBelongingToMainEntity, List<SideEffectDto>>(
            new GetSideEffectsBelongingToMainEntity(mainEntityId),
            context =>
            {
                context.SetTimeout(SideEffectsTimeout);
                return ValueTask.CompletedTask;
            },
            cancellationToken);

        return sideEffects
            .TapError(error => _logger.LogWarning("Side effects of {MainEntityId} are unavailable: {Error}", mainEntityId, error.Code))
            .OrElse(error => error.Code == RequestErrorCodes.Timeout
                ? Result.Success(new List<SideEffectDto>())
                : Result.Failure<List<SideEffectDto>>(error));
    }
}
