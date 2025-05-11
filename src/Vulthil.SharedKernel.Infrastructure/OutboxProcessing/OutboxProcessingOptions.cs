using System.ComponentModel.DataAnnotations;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

public sealed class OutboxProcessingOptions
{
    [Range(1, 100)]
    public int OutboxProcessingDelayInSeconds { get; init; } = 7;
    [Range(1, int.MaxValue)]
    public int BatchSize { get; init; } = 10;
}
