namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable summary of a single continuous pipeline execution cycle.
/// </summary>
public record PipelineCycleResult(
    int IngestedCount,
    int EvaluatedCount,
    int PacketsGenerated,
    int PacketsSkipped,
    int FailedCount,
    DateTime CompletedAtUtc);
