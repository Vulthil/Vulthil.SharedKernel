using WebApi.Domain.SideEffects;

namespace WebApi.Application.SideEffects;

/// <summary>
/// Represents the SideEffectDto.
/// </summary>
public sealed record SideEffectDto
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Guid Id { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Guid MainEntityId { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public required Status Status { get; init; }

    /// <summary>
    /// Executes this member.
    /// </summary>
    public static SideEffectDto FromModel(SideEffect sideEffect) => new()
    {
        Id = sideEffect.Id.Value,
        MainEntityId = sideEffect.MainEntityId,
        Status = sideEffect.Status
    };
}
