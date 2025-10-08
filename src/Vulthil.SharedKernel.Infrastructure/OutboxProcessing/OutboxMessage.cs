namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid GroupId { get; init; }
    public required string Type { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset OccurredOnUtc { get; init; }
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
