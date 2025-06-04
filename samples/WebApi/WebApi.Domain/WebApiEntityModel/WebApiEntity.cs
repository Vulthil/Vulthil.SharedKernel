using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;
using WebApi.Domain.WebApiEntityModel.Events;

namespace WebApi.Domain.WebApiEntityModel;

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

    public void UpdateName(string name)
    {
        Name = name;
        Raise(new WebApiEntityNameUpdatedEvent(Id, Name));
    }
}

public static class WebApiEntityErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("WebApiEntity.NotFound", $"Entity with Id {id} was not found.");
}
