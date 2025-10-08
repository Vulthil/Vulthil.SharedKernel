using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.MainEntities.GetById;

public sealed record GetMainEntityByIdQuery(Guid Id) : IQuery<Result<MainEntityDto>>;
public sealed record MainEntityDto
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    private MainEntityDto(Guid id, string name) => (Id, Name) = (id, name);
    internal static MainEntityDto FromModel(MainEntity mainEntity) =>
        new(mainEntity.Id.Value, mainEntity.Name);
}

public sealed class GetMainEntityQueryHandler(ILogger<GetMainEntityQueryHandler> logger, IWebApiDbContext dbContext) : IQueryHandler<GetMainEntityByIdQuery, Result<MainEntityDto>>
{
    private readonly ILogger<GetMainEntityQueryHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;

    public async Task<Result<MainEntityDto>> HandleAsync(GetMainEntityByIdQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying MainEntity With Id: {Id}", query.Id);

        var id = new MainEntityId(query.Id);
        return await _dbContext.MainEntities.FirstOrDefaultAsync(w => w.Id == id)
            .ToResultAsync(MainEntityErrors.NotFound(query.Id))
            .MapAsync(MainEntityDto.FromModel);
    }
}
