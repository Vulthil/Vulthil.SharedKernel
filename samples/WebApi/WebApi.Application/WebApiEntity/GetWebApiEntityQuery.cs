using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using WebApi.Domain.WebApiEntityModel;

namespace WebApi.Application.WebApiEntity;

public sealed record GetWebApiEntityQuery(Guid Id) : IQuery<Result<WebApiEntityDto>>;
public sealed record WebApiEntityDto
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    private WebApiEntityDto(Guid id, string name) => (Id, Name) = (id, name);
    internal static WebApiEntityDto FromModel(Domain.WebApiEntityModel.WebApiEntity webApiEntity) =>
        new(webApiEntity.Id.Value, webApiEntity.Name);
}

public sealed class GetWebApiEntityQueryHandler(ILogger<GetWebApiEntityQueryHandler> logger, IWebApiDbContext dbContext) : IQueryHandler<GetWebApiEntityQuery, Result<WebApiEntityDto>>
{
    private readonly ILogger<GetWebApiEntityQueryHandler> _logger = logger;
    private readonly IWebApiDbContext _dbContext = dbContext;

    public async Task<Result<WebApiEntityDto>> HandleAsync(GetWebApiEntityQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying WebApiEntity With Id: {Id}", query.Id);

        var webApiEntityId = new WebApiEntityId(query.Id);
        var entity = await _dbContext.WebApiEntities.FirstOrDefaultAsync(w => w.Id == webApiEntityId);

        return entity
            .ToResult(WebApiEntityErrors.NotFound(query.Id))
            .Map(WebApiEntityDto.FromModel);
    }
}
