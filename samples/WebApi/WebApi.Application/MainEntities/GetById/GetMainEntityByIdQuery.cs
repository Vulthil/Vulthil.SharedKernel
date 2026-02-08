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

public sealed class GetMainEntityQueryHandler(ILogger<GetMainEntityQueryHandler> logger, IWebApiDbContext dbContext, IRequester requester) : IQueryHandler<GetMainEntityByIdQuery, Result<MainEntityDto>>
{
    private readonly ILogger<GetMainEntityQueryHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;
    private readonly IRequester _requester = requester;

    public async Task<Result<MainEntityDto>> HandleAsync(GetMainEntityByIdQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying MainEntity With Id: {Id}", query.Id);

        var id = new MainEntityId(query.Id);
        return await _dbContext.MainEntities.FirstOrDefaultAsync(w => w.Id == id)
            .ToResultAsync(MainEntityErrors.NotFound(query.Id))
            .BindAsync(m => _requester.RequestAsync<GetSideEffectsBelongingToMainEntity, List<SideEffectDto>>(new GetSideEffectsBelongingToMainEntity(id.Value), cancellationToken: cancellationToken)
                .MapAsync(sideEffects => (m, sideEffects)))
            .MapAsync(tuple => MainEntityDto.FromModel(tuple.m, tuple.sideEffects));
    }
}
