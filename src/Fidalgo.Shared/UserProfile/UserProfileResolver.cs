using Microsoft.Extensions.Logging;

namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Resolves the on-disk path for user career profile files
/// (resume, career story, career goals) consumed by the agent and host pipelines.
///
/// A path supplied explicitly wins when the file exists. Otherwise, when no explicit
/// <c>baseDir</c> is provided, the resolver checks <c>Documents\Fidalgo\</c> first and
/// falls back to the legacy <c>%APPDATA%\.config\Fidalgo\</c> directory with an
/// informational log message. When an explicit <c>baseDir</c> is provided, only that
/// directory is searched to preserve strict isolation.
/// </summary>
public class UserProfileResolver
{
    private readonly string _primaryDir;
    private readonly string? _legacyDir;
    private readonly ILogger<UserProfileResolver> _logger;

    /// <summary>Initializes a new instance of the <see cref="UserProfileResolver"/>.</summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="baseDir">Override for the default base directory; when specified, only this directory is searched.</param>
    public UserProfileResolver(ILogger<UserProfileResolver> logger, string? baseDir = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            _primaryDir = baseDir;
            _legacyDir = null;
        }
        else
        {
            _primaryDir = UserProfilePaths.GetDefaultDocumentsDir();
            _legacyDir = UserProfilePaths.GetDefaultBaseDir();
        }
    }

    internal UserProfileResolver(ILogger<UserProfileResolver> logger, string primaryDir, string? legacyDir)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryDir);

        _logger = logger;
        _primaryDir = primaryDir;
        _legacyDir = legacyDir;
    }

    /// <summary>Resolves the on-disk path for the user's resume file, or null if not found.</summary>
    /// <param name="cliPath">Path supplied via CLI or override, or null when omitted.</param>
    public string? ResolveResumePath(string? cliPath) =>
        ResolvePath(cliPath, UserProfilePaths.ResumeFileName, "resume");

    /// <summary>Resolves the on-disk path for the user's career story file, or null if not found.</summary>
    /// <param name="cliPath">Path supplied via CLI or override, or null when omitted.</param>
    public string? ResolveCareerStoryPath(string? cliPath) =>
        ResolvePath(cliPath, UserProfilePaths.CareerStoryFileName, "career story");

    /// <summary>Resolves the on-disk path for the user's career goals file, or null if not found.</summary>
    /// <param name="cliPath">Path supplied via CLI or override, or null when omitted.</param>
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

        var primaryPath = Path.Combine(_primaryDir, defaultFileName);
        if (File.Exists(primaryPath))
        {
            return primaryPath;
        }

        if (!string.IsNullOrWhiteSpace(_legacyDir))
        {
            var legacyPath = Path.Combine(_legacyDir, defaultFileName);
            if (File.Exists(legacyPath))
            {
                _logger.LogInformation(
                    "Loaded {FriendlyName} from legacy config directory {LegacyPath}. Consider moving profile documents to {PrimaryDir}.",
                    friendlyName,
                    legacyPath,
                    _primaryDir);
                return legacyPath;
            }
        }

        return null;
    }
}
