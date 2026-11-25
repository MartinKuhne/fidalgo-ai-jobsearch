namespace Fidalgo.Host.Models;

/// <summary>
/// Server-side user profile defaults and readiness flags returned by <c>GET /api/profile/defaults</c>.
/// </summary>
public sealed record ProfileDefaultsDto(
    string? DefaultEmail,
    bool HasResume,
    bool HasCareerStory,
    int CareerStoryLength,
    bool HasCareerGoals,
    int CareerGoalsLength,
    string? BaseResumeMarkdown,
    string? CareerStoryMarkdown,
    string? CareerGoalsMarkdown,
    string OutputDirectory);
