namespace Fidalgo.Shared.Models;

/// <summary>
/// Optional file-path or inline-content overrides when loading a user's career profile.
/// Inline content overrides take precedence over file paths.
/// </summary>
public sealed record UserProfileLoadOverrides(
    string? ResumePath = null,
    string? CareerStoryPath = null,
    string? CareerGoalsPath = null,
    string? ResumeContentOverride = null,
    string? CareerStoryContentOverride = null,
    string? CareerGoalsContentOverride = null,
    string? OutputDirectoryOverride = null);
