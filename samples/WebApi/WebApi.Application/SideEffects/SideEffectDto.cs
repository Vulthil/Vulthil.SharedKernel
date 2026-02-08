using WebApi.Domain.SideEffects;

namespace WebApi.Application.SideEffects;

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
