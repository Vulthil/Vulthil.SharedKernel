using Microsoft.EntityFrameworkCore;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;
using WebApi.Data;

namespace WebApi.Models;

public sealed record WebApiEntityId(Guid Value);

public class WebApiEntity : AggregateRoot<WebApiEntityId>
{
    public string Name { get; private set; }
    private WebApiEntity(string name) : base(new(Guid.CreateVersion7())) => Name = name;
    public static WebApiEntity Create(string name)
    {
        var webApiEntity = new WebApiEntity(name);
        webApiEntity.Raise(new WebApiEntityCreatedEvent(webApiEntity.Id));

        return webApiEntity;
    }

    internal void UpdateName(string name)
    {
        Name = name;
        Raise(new WebApiEntityNameUpdatedEvent(Id, Name));
    }
}

public sealed record WebApiEntityNameUpdatedEvent(WebApiEntityId Id, string Name) : IDomainEvent;
public sealed record WebApiEntityCreatedEvent(WebApiEntityId Id) : IDomainEvent;

public sealed class WebApiEntityCreatedEventHandler(ILogger<WebApiEntityCreatedEventHandler> logger) : IDomainEventHandler<WebApiEntityCreatedEvent>, IDomainEventHandler<WebApiEntityNameUpdatedEvent>
{
    private readonly ILogger<WebApiEntityCreatedEventHandler> _logger = logger;

    public Task HandleAsync(WebApiEntityCreatedEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebApiEntityCreated: {Id}", notification.Id.Value);
        return Task.CompletedTask;
    }

    public Task HandleAsync(WebApiEntityNameUpdatedEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebApiEntity Name updated to: {Name}", notification.Name);
        return Task.CompletedTask;
    }
}
public sealed record WebApiEntityDto
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    private WebApiEntityDto(Guid id, string name) => (Id, Name) = (id, name);
    internal static WebApiEntityDto FromModel(WebApiEntity webApiEntity) =>
        new(webApiEntity.Id.Value, webApiEntity.Name);
}

public sealed record GetWebApiEntityQuery(Guid Id) : IQuery<WebApiEntityDto>;
public sealed class GetWebApiEntityQueryHandler(ILogger<GetWebApiEntityQueryHandler> logger, WebApiDbContext dbContext) : IQueryHandler<GetWebApiEntityQuery, WebApiEntityDto>
{
    private readonly ILogger<GetWebApiEntityQueryHandler> _logger = logger;
    private readonly WebApiDbContext _dbContext = dbContext;

    public async Task<Result<WebApiEntityDto>> HandleAsync(GetWebApiEntityQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying WebApiEntity With Id: {Id}", query.Id);

        var webApiEntityId = new WebApiEntityId(query.Id);
        var entity = await _dbContext.WebApiEntities.FirstOrDefaultAsync(w => w.Id == webApiEntityId);

        return entity is not null
            ? Result.Success(entity).Map(WebApiEntityDto.FromModel)
            : Result.Failure<WebApiEntityDto>(WebApiEntityErrors.NotFound(query.Id));
    }
}
public sealed record CreateWebApiEntityCommand(string Name) : ITransactionalCommand<Guid>;

public sealed class CreateWebApiEntityCommandHandler(ILogger<CreateWebApiEntityCommandHandler> logger, WebApiDbContext dbContext) : ICommandHandler<CreateWebApiEntityCommand, Guid>
{
    private readonly ILogger<CreateWebApiEntityCommandHandler> _logger = logger;
    private readonly WebApiDbContext _dbContext = dbContext;

    public async Task<Result<Guid>> HandleAsync(CreateWebApiEntityCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating WebApiEntity With name: {Name}", command.Name);

        var webApiEntity = WebApiEntity.Create(command.Name);
        _dbContext.WebApiEntities.Add(webApiEntity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return webApiEntity.Id.Value;
    }
}
public sealed record UpdateWebApiEntityCommand(Guid Id, string Name) : ITransactionalCommand;
public sealed class UpdateWebApiEntityCommandHandler(ILogger<UpdateWebApiEntityCommandHandler> logger, WebApiDbContext dbContext) : ICommandHandler<UpdateWebApiEntityCommand>
{
    private readonly ILogger<UpdateWebApiEntityCommandHandler> _logger = logger;
    private readonly WebApiDbContext _dbContext = dbContext;

    public async Task<Result> HandleAsync(UpdateWebApiEntityCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating WebApiEntity With name: {Name}", command.Name);

        var webEntityId = new WebApiEntityId(command.Id);
        var entity = await _dbContext.WebApiEntities.FirstOrDefaultAsync(w => w.Id == webEntityId);
        if (entity is null)
        {
            return Result.Failure(WebApiEntityErrors.NotFound(command.Id));
        }

        entity.UpdateName(command.Name);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public static class WebApiEntityErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("WebApiEntity.NotFound", $"Entity with Id {id} was not found.");
}
