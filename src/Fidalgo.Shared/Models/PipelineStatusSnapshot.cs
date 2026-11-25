using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.Models;

/// <summary>
/// Immutable snapshot of the continuous background pipeline status for observability endpoints.
/// </summary>
public record PipelineStatusSnapshot(
    bool Enabled,
    double IntervalHours,
    int MinScore,
    string RecommendationFilter,
    bool IsRunning,
    DateTime? LastCycleStartedAtUtc,
    DateTime? LastCycleCompletedAtUtc,
    PipelineCycleResult? LastResult,
    string? LastError);
