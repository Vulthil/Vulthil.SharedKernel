using Vulthil.SharedKernel.Primitives;

namespace WebApi.Models;

public sealed record WebApiEntityId(Guid Value);

public class WebApiEntity : Entity<WebApiEntityId>
{
    public string Name { get; private set; }
    public WebApiEntity(string name) : base(new(Guid.NewGuid())) => Name = name;
    public static WebApiEntity Create(string name) => new(name);
}

