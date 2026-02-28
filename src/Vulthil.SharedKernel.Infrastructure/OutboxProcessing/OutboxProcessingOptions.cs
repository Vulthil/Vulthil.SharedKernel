using System.ComponentModel.DataAnnotations;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

public sealed class OutboxProcessingOptions
{
    [Range(1, 100)]
    public int OutboxProcessingDelayInSeconds { get; init; } = 2;
    [Range(1, 300)]
    public int MaxDelaySeconds { get; init; } = 60;
    [Range(1, int.MaxValue)]
    public int BatchSize { get; init; } = 10;
    [Range(1, int.MaxValue)]
    public int MaxRetries { get; init; } = 3;
    public bool EnableParallelPublishing { get; init; }
}
