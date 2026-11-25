namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Default file names and on-disk locations for the user career profile
/// (resume, career story, career goals) and generated application packets.
/// </summary>
public static class UserProfilePaths
{
    /// <summary>Default file name for the candidate's resume.</summary>
    public const string ResumeFileName = "resume.md";

    /// <summary>Default file name for the candidate's career story.</summary>
    public const string CareerStoryFileName = "career-story.md";

    /// <summary>Default file name for the candidate's career goals.</summary>
    public const string CareerGoalsFileName = "career-goals.md";

    /// <summary>Subdirectory name for generated per-job application packets.</summary>
    public const string ApplicationsDirectoryName = "applications";

    /// <summary>
    /// Returns the default configuration directory for the user profile on Windows,
    /// which is <c>%APPDATA%\.config\Fidalgo</c>. Used for <c>config.json</c> and as
    /// a legacy fallback for profile documents. The directory is not created here.
    /// </summary>
    public static string GetDefaultBaseDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, ".config", "Fidalgo");
    }

    /// <summary>
    /// Returns the default documents directory for user profile files (<c>Documents\Fidalgo</c>).
    /// Falls back to <c>{UserProfile}\Documents\Fidalgo</c> when <see cref="Environment.SpecialFolder.MyDocuments"/> is empty.
    /// </summary>
    public static string GetDefaultDocumentsDir() =>
        ResolveDocumentsDir(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>
    /// Returns the default base directory for generated per-job application packets
    /// (<c>Documents\Fidalgo\applications</c>).
    /// </summary>
    public static string GetDefaultApplicationsDir() =>
        Path.Combine(GetDefaultDocumentsDir(), ApplicationsDirectoryName);

    internal static string ResolveDocumentsDir(string? myDocuments, string? userProfile)
    {
        var documentsRoot = !string.IsNullOrWhiteSpace(myDocuments)
            ? myDocuments
            : Path.Combine(userProfile ?? string.Empty, "Documents");

        return Path.Combine(documentsRoot, "Fidalgo");
    }
}
