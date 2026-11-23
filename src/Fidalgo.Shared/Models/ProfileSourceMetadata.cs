namespace Fidalgo.Shared.Models;

/// <summary>
/// Metadata describing where each user profile document was resolved from.
/// </summary>
public sealed record ProfileSourceMetadata(
    bool ResumeFromOverride,
    string? ResumePath,
    bool HasCareerStory,
    string? CareerStoryPath,
    bool HasCareerGoals,
    string? CareerGoalsPath);
