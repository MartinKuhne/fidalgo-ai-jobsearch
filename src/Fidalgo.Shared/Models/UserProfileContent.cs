namespace Fidalgo.Shared.Models;

/// <summary>
/// Loaded user career profile content and resolved configuration metadata.
/// </summary>
public sealed record UserProfileContent(
    string ResumeMarkdown,
    string? CareerStoryMarkdown,
    string? CareerGoalsMarkdown,
    string? DefaultEmail,
    string ApplicationsOutputDir,
    ProfileSourceMetadata Sources);
