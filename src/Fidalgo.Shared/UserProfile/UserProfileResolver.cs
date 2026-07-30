using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Resolves the on-disk path for optional user career profile files
/// (career story, career goals) consumed by the agent evaluation pipeline.
///
/// A path supplied on the command line wins when the file exists. Otherwise
/// the resolver falls back to the default location under
/// <c>%APPDATA%\.config\Fidalgo</c>. When the caller explicitly passes a path
/// that does not exist, a warning is logged so the user is not silently
/// switched to the default.
/// </summary>
public class UserProfileResolver
{
    private readonly string _baseDir;
    private readonly ILogger<UserProfileResolver> _logger;

    /// <summary>Initializes a new instance of the UserProfileResolver.</summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="baseDir">Override for the default base directory; useful in tests.</param>
    public UserProfileResolver(ILogger<UserProfileResolver> logger, string? baseDir = null)
    {
        _baseDir = baseDir ?? UserProfilePaths.GetDefaultBaseDir();
        _logger = logger;
    }

    /// <summary>Resolves the on-disk path for the user's resume file, or null if neither the CLI path nor the default file exists.</summary>
    /// <param name="cliPath">Path supplied on the command line, or null when the user did not pass the flag.</param>
    public string? ResolveResumePath(string? cliPath) =>
        ResolvePath(cliPath, UserProfilePaths.ResumeFileName, "resume");

    /// <summary>Resolves the on-disk path for the user's career story file, or null if neither the CLI path nor the default file exists.</summary>
    /// <param name="cliPath">Path supplied on the command line, or null when the user did not pass the flag.</param>
    public string? ResolveCareerStoryPath(string? cliPath) =>
        ResolvePath(cliPath, UserProfilePaths.CareerStoryFileName, "career story");

    /// <summary>Resolves the on-disk path for the user's career goals file, or null if neither the CLI path nor the default file exists.</summary>
    /// <param name="cliPath">Path supplied on the command line, or null when the user did not pass the flag.</param>
    public string? ResolveCareerGoalsPath(string? cliPath) =>
        ResolvePath(cliPath, UserProfilePaths.CareerGoalsFileName, "career goals");

    private string? ResolvePath(string? cliPath, string defaultFileName, string friendlyName)
    {
        if (!string.IsNullOrWhiteSpace(cliPath))
        {
            if (File.Exists(cliPath))
            {
                return cliPath;
            }

            _logger.LogWarning(
                "User-supplied {FriendlyName} path does not exist: {Path}. Falling back to the default location.",
                friendlyName,
                cliPath);
        }

        var defaultPath = Path.Combine(_baseDir, defaultFileName);
        return File.Exists(defaultPath) ? defaultPath : null;
    }
}
