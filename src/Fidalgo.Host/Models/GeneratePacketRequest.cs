namespace Fidalgo.Host.Models;

/// <summary>
/// Request payload for generating or regenerating a job's application packet.
/// </summary>
public sealed record GeneratePacketRequest(
    string? BaseResumeOverride = null,
    string? CareerStoryOverride = null,
    string? CareerGoalsOverride = null,
    bool Force = false);
