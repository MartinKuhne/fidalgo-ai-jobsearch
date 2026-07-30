namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Default file names and on-disk location for the user career profile
/// (career story and career goals) used by the agent evaluation pipeline.
/// </summary>
public static class UserProfilePaths
{
    /// <summary>Default file name for the candidate's resume.</summary>
    public const string ResumeFileName = "resume.md";

    /// <summary>Default file name for the candidate's career story.</summary>
    public const string CareerStoryFileName = "career-story.md";

    /// <summary>Default file name for the candidate's career goals.</summary>
    public const string CareerGoalsFileName = "career-goals.md";

    /// <summary>
    /// Returns the default base directory for the user career profile on Windows,
    /// which is <c>%APPDATA%\.config\Fidalgo</c>. The directory is not created
    /// here; callers should treat the path as a location to look in.
    /// </summary>
    public static string GetDefaultBaseDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, ".config", "Fidalgo");
    }
}
